﻿using HlidacStatu.Entities;


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nest;

namespace HlidacStatu.Repositories
{
    public static class FilteredIdsRepo
    {
        public class QueryBatch
        {
            public string TaskPrefix { get; set; } = string.Empty;

            public string Query { get; set; }
            public Action<string> LogOutputFunc { get; set; } = null;
            public Action<Devmasters.Batch.ActionProgressData> ProgressOutputFunc { get; set; } = null;

        }
        
        public static class CachedIds
        {

            private static volatile Devmasters.Cache.Elastic.Manager<string[], QueryBatch> cacheSmlouvy
                = Devmasters.Cache.Elastic.Manager<string[], QueryBatch>.GetSafeManagerInstance(
                    "cachedIdsSmlouvy",
                    q => GetSmlouvyAsync(q).ConfigureAwait(false).GetAwaiter().GetResult(),
                    TimeSpan.FromHours(24),
                    Devmasters.Config.GetWebConfigValue("ESConnection").Split(';'),
                    "DevmastersCache", null, null,
                    key => key.TaskPrefix + Devmasters.Crypto.Hash.ComputeHashToHex(key.Query)
                    );

            public static string[] Smlouvy(QueryBatch query, bool forceUpdate = false)
            {
                if (forceUpdate)
                {
                    cacheSmlouvy.Delete(query);
                }

                return cacheSmlouvy.Get(query);

            }


            
        }

        public static async Task<string[]> GetSmlouvyAsync(QueryBatch query)
        {
            var stack = HlidacStatu.Util.StackReport.GetCallingMethod(true);

            if (query == null)
                return new string[] { };

            if (string.IsNullOrEmpty(query.Query))
                return new string[] { };

            Func<int, int, Task<ISearchResponse<Smlouva>>> searchFunc = async (size, page) =>
            {
                var client = await ES.Manager.GetESClientAsync();
                return await client.SearchAsync<Smlouva>(a => a
                    .Size(size)
                    .Source(false)
                    .From(page * size)
                    .Query(q => SmlouvaRepo.Searching.GetSimpleQuery(query.Query))
                    .Scroll("1m")
                );
            };

            List<string> ids2Process = new List<string>();
            await Repositories.Searching.Tools.DoActionForQueryAsync<Smlouva>(await ES.Manager.GetESClientAsync(),
                searchFunc, (hit, param) =>
                {
                    ids2Process.Add(hit.Id);
                    return new Devmasters.Batch.ActionOutputData() { CancelRunning = false, Log = null };
                }, null, query.LogOutputFunc, query.ProgressOutputFunc, false);

            return ids2Process.ToArray();
        }
    }
}

