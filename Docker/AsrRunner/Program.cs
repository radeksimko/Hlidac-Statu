﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AsrRunner;
using Devmasters.Log;
using FluentFTP;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(Global.MinLogLevel)
    .WriteTo.Console()
    .AddLogStash(new Uri(Global.LogStashUrl))
    .Enrich.WithProperty("hostname", Global.Hostname)
    .Enrich.WithProperty("codeversion", System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString())
    .Enrich.WithProperty("application_name", "AsrRunner")
    .CreateLogger();
    
var logger = Log.ForContext<Program>();
var failSave = new FailSaveCounter(5);
    
// Graceful shutdown
logger.Information("Starting up...");
var applicationCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, consoleCancelEventArgs) =>
{
    // Tell .NET to not terminate the process
    consoleCancelEventArgs.Cancel = true;

    logger.Information("Received SIGINT (Ctrl+C)");
    if (!applicationCts.IsCancellationRequested)
    {
        applicationCts.Cancel();
    }
};
AppDomain.CurrentDomain.ProcessExit += (_, _) =>
{
    logger.Information("Received SIGTERM");
    if (!applicationCts.IsCancellationRequested)
    {
        applicationCts.Cancel();
    }
};

// Necessary configuration
logger.Debug("Setting up services");
using var taskQueueService = new TaskQueueService(logger);

using var ftpClient = new FtpClient(Global.FtpAddress, Global.FtpPort, Global.FtpUserName, Global.FtpPassword);
ftpClient.RetryAttempts = 5;
ftpClient.NoopInterval = 30_000;
ftpClient.EncryptionMode = FtpEncryptionMode.Implicit;
ftpClient.ValidateAnyCertificate = true;

// Application loop
logger.Debug("Running main code");
while (!applicationCts.IsCancellationRequested)
{
    // create directory
    Directory.CreateDirectory(Global.LocalDirectoryPath);
    
    QueueItem? queueItem;
    
    try
    {
        queueItem = await taskQueueService.GetNewTaskAsync(applicationCts.Token);
    }
    catch (Exception e)
    {
        logger.Error(e, "Error occured in GetNewTaskAsync");
        await Task.Delay(Global.DelayIfServerHasNoTaskInSec * 1000);
        continue;
    }

    if (queueItem is null)
    {
        await Task.Delay(Global.DelayIfServerHasNoTaskInSec * 1000);
        continue;
    }

    logger.Information("New task with queue item {queueItem} received.", queueItem);
    string inputFileLocal = $"{Global.LocalDirectoryPath}/{queueItem.ItemId}.{Global.InputFileExtension}";
    string inputFileFtp = $"/{queueItem.Dataset}/{queueItem.ItemId}.{Global.InputFileExtension}";
    string outputFileLocal = $"{Global.LocalDirectoryPath}/{queueItem.ItemId}.{Global.OutputFileExtension}";
    string outputFileFtp = $"/{queueItem.Dataset}/{queueItem.ItemId}.{Global.OutputFileExtension}";
    
    try
    {
        logger.Debug("Checking ftp if task [{fileName}] was completed before.", outputFileFtp);
        await ftpClient.ConnectAsync(applicationCts.Token);
        if (await ftpClient.FileExistsAsync(outputFileFtp))
        {
            logger.Information("This task [{fileName}] was already completed before.", outputFileFtp);
            await taskQueueService.ReportSuccessAsync(applicationCts.Token);
            continue;
        }

        logger.Debug("Downloading [{fileName}] file from ftp", inputFileFtp);
        await ftpClient.DownloadFileAsync(inputFileLocal, inputFileFtp);
        await ftpClient.DisconnectAsync(applicationCts.Token);
        logger.Debug("File downloaded.");

        // run ASR
        var processResult = await "cd /opt/app/ && ./process.sh".Bash(logger);
        if (processResult != 0)
        {
            logger.Error("ASR resulted with error. Returned {processResult}.", processResult);
        }

        logger.Debug("Uploading [{fileName}] file to ftp", outputFileLocal);
        await ftpClient.ConnectAsync(applicationCts.Token);
        await ftpClient.UploadFileAsync(outputFileLocal, outputFileFtp);

        logger.Debug("File [{fileName}] uploaded to ftp", outputFileLocal);
        long fileSize = (new FileInfo(outputFileLocal)).Length;
        long uploadedFileSize = await ftpClient.GetFileSizeAsync(outputFileFtp);
        await ftpClient.DisconnectAsync(applicationCts.Token);
        
        if (fileSize == uploadedFileSize)
        {
            await taskQueueService.ReportSuccessAsync(applicationCts.Token);
            logger.Information("Task [{fileName}] successfully completed.", outputFileFtp);
            failSave.ResetCounter(); // everything went right
        }
        else
        {
            logger.Warning("Task [{fileName}] failed. FileSize {fileSize} != UploadedSize {uploadedFileSize}",
                outputFileFtp, fileSize, uploadedFileSize);
            throw new Exception("Uploaded size is not equal to the sent size.");
        }

    }
    catch (Exception exception)
    {
        logger.Warning(exception, "Task [{fileName}] failed.", outputFileFtp);
        await taskQueueService.ReportFailureAsync(applicationCts.Token);
        failSave.ReportFail(); //If too many fails occurs, it throws an exception
    }
    finally
    {
        logger.Debug("Cleaning local folder [{folder}]", Global.LocalDirectoryPath);
        Directory.Delete(Global.LocalDirectoryPath, true);
        
        Cleanup();
    }
}

logger.Information("Terminating application.");
Log.CloseAndFlush();

void Cleanup()
{
    logger.Debug("Running cleanup of ASR folders");
    // this needs to be cleaned up, because if problem occurs in process.sh (from Czech-ASR)
    // then these commands are not run and it goes to the fail spiral
    var model_dir= "/opt/app/exp/chain/cnn_tdnn";
    Directory.Delete("/opt/app/data/test", true);
    Directory.Delete("/opt/app/tmp", true);
    Directory.Delete("/opt/app/exp/nnet3/ivectors", true);
    Directory.Delete($"{model_dir}/decode", true);
    Directory.Delete($"{model_dir}/rescore", true);
    logger.Debug("cleanup finished");
}
