﻿using Microsoft.AspNetCore.Html;

using System.Collections.Generic;
using System.Linq;

namespace HlidacStatu.XLib.Render
{

    public class ReportModel
    {
        public class QueueItem
        {
            public enum types { chart, table }
            public string Title { get; set; }
            public string Key { get; set; }
            public object Data { get; set; }
            public types Type { get; set; }
        }

        public void Add(string key, string title, IHtmlContent chart)
        {
            queue.Add(new QueueItem() { Key = key, Title = title, Data = chart, Type = QueueItem.types.chart });
        }
        public void Add(string key, string title, ReportDataSource report)
        {
            queue.Add(new QueueItem() { Key = key, Title = title, Data = report, Type = QueueItem.types.table });
        }
        public void Add<T>(string key, string title, ReportDataSource<T> report)
        //where T : class
        {
            queue.Add(new QueueItem() { Key = key, Title = title, Data = report, Type = QueueItem.types.table });
        }

        private List<QueueItem> queue = new List<QueueItem>();

        public IEnumerable<QueueItem> GetQueue()
        {
            return queue;
        }

        public IEnumerable<QueueItem> GetQueue(params string[] onlyKeys)
        {
            List<string> keys = onlyKeys.ToList();

            return queue
                .Where(k => keys.Contains(k.Key))
                .OrderBy(o => keys.IndexOf(o.Key))
                ;

        }
    }

}
