using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FullTextSearch;
using HlidacStatu.Entities;

namespace HlidacStatu.AutocompleteApi.Services
{
    public interface IMemoryStoreService
    {
        Index<Autocomplete> HlidacFulltextIndex { get; }
        Index<Autocomplete> SmallSampleIndex { get; }
        DateTime RunningSince { get; }
        DateTime LastDataRenewalStarted { get; }
        bool IsDataRenewalRunning { get; }
        Exception LastException { get; }
        Task GenerateAll(CancellationToken cancellationToken = default);
    }

    public class MemoryStoreService : IMemoryStoreService
    {
        private readonly HlidacApiService _hlidacService;
        
        // Search indexes
        public Index<Autocomplete> HlidacFulltextIndex { get; private set; }

        private readonly object _fullTextLock = new();

        public Index<Autocomplete> SmallSampleIndex { get; private set; }

        private readonly object _sampleLock = new();

        // metadata
        public DateTime RunningSince { get; }
        public DateTime LastDataRenewalStarted { get; private set; }
        public bool IsDataRenewalRunning { get; private set; }
        public Exception LastException { get; private set; }

        public MemoryStoreService(HlidacApiService hlidacService)
        {
            _hlidacService = hlidacService;
            
            RunningSince = DateTime.Now;
            //quick load from backups so app can restart fast
            HlidacFulltextIndex = LoadFromBackup<Autocomplete>(nameof(HlidacFulltextIndex));
            SmallSampleIndex = LoadFromBackup<Autocomplete>(nameof(SmallSampleIndex));

            #pragma warning disable 4014
            _ = GenerateAll(); // fire and forget - generate data on background
            #pragma warning restore 4014
        }

        public async Task GenerateAll(CancellationToken cancellationToken = default)
        {
            // prevent running twice at the same time
            if (IsDataRenewalRunning)
                return;
            
            IsDataRenewalRunning = true;
            LastDataRenewalStarted = DateTime.Now;
            try
            {
                // add new tasks here
                await Task.WhenAll(
                    GenerateHlidacFulltextIndex(cancellationToken),
                    GenerateSmallSampleIndex(cancellationToken)
                );
                LastException = null;
            }
            catch (Exception e)
            {
                LastException = e;
            }
            finally
            {
                IsDataRenewalRunning = false;
            }
        }


        private async Task GenerateHlidacFulltextIndex(CancellationToken cancellationToken = default)
        {
            var autocompleteBinary = await _hlidacService.LoadFullAutocomplete();
            lock (_fullTextLock)
            {
                HlidacFulltextIndex = Index<Autocomplete>.Deserialize(autocompleteBinary);
            }

            await CreateBackup(nameof(HlidacFulltextIndex), HlidacFulltextIndex, cancellationToken);
        }

        private async Task GenerateSmallSampleIndex(CancellationToken cancellationToken = default)
        {
            string _asdf = $@"[
              {{
                ""id"": ""ico:00006947"",
                ""text"": ""Ministerstvo financí"",
                ""imageElement"": ""fas fa-university"",
                ""type"": ""úřad"",
                ""description"": ""Hlavní město Praha"",
                ""priority"": 2
              }},
              {{
                ""id"": ""ico:28569113"",
                ""text"": ""MINORR FINANCE a.s."",
                ""imageElement"": ""fas fa-industry-alt"",
                ""type"": ""firma"",
                ""description"": ""Moravskoslezský kraj"",
                ""priority"": 0
              }},
              {{
                ""id"": ""ico:27415414"",
                ""text"": ""MINT Financial Services, s.r.o., v likvidaci"",
                ""imageElement"": ""fas fa-industry-alt"",
                ""type"": ""firma"",
                ""description"": ""Hlavní město Praha"",
                ""priority"": 0
              }},
              {{
                ""id"": ""ico:48137430"",
                ""text"": ""Ministerstvo financí České republiky Generální ředitelství cel"",
                ""imageElement"": ""fas fa-industry-alt"",
                ""type"": ""firma"",
                ""description"": """",
                ""priority"": 0
              }},
              {{
                ""id"": ""ico:00001376"",
                ""text"": ""FEDERÁLNÍ MINISTERSTVO FINANCÍ"",
                ""imageElement"": ""fas fa-industry-alt"",
                ""type"": ""firma"",
                ""description"": ""vygenerováno v {DateTime.Now}"",
                ""priority"": 0
              }}
            ]";

            lock (_sampleLock)
            {
                var acObject = Newtonsoft.Json.JsonConvert.DeserializeObject<Entities.Autocomplete[]>(_asdf);
                SmallSampleIndex = new FullTextSearch.Index<Autocomplete>(acObject);
            }

            await CreateBackup(nameof(SmallSampleIndex), SmallSampleIndex, cancellationToken);
        }


        private async Task CreateBackup<T>(string indexName, Index<T> index, CancellationToken cancellationToken)
            where T : IEquatable<T>
        {
            string filename = AssembleFileName(indexName);
            await using var filestream = File.Create(filename);
            await filestream.WriteAsync(index.Serialize(), cancellationToken);
        }
        
        private Index<T> LoadFromBackup<T>(string indexName)
            where T : IEquatable<T>
        {
            string filename = AssembleFileName(indexName);
            if (File.Exists(filename))
            {
                var bytes = File.ReadAllBytes(filename);
                if (bytes.Length > 0)
                {
                    return Index<T>.Deserialize(bytes);
                }
            }

            return null;
        }

        private string AssembleFileName(string indexName)
        {
            return $"{indexName}.bak";
        }

        
    }

}