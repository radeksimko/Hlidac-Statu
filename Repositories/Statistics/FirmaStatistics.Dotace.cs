﻿using HlidacStatu.Entities;
using HlidacStatu.Extensions;
using HlidacStatu.Lib.Analytics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HlidacStatu.Repositories.Statistics
{
    public static partial class FirmaStatistics
    {
        static Devmasters.Cache.Couchbase.Manager<StatisticsSubjectPerYear<Firma.Statistics.Dotace>, Firma>
            _dotaceCache = Devmasters.Cache.Couchbase.Manager<StatisticsSubjectPerYear<Firma.Statistics.Dotace>, Firma>
                .GetSafeInstance("Firma_DotaceStatistics_v2",
                    (firma) => CalculateDotaceStatAsync(firma).ConfigureAwait(false).GetAwaiter().GetResult(),
                    TimeSpan.FromDays(7),
                    Devmasters.Config.GetWebConfigValue("CouchbaseServers").Split(','),
                    Devmasters.Config.GetWebConfigValue("CouchbaseBucket"),
                    Devmasters.Config.GetWebConfigValue("CouchbaseUsername"),
                    Devmasters.Config.GetWebConfigValue("CouchbasePassword"),
                    f => f.ICO);


        static Devmasters.Cache.Couchbase.Manager<StatisticsSubjectPerYear<Firma.Statistics.Dotace>, (Firma firma,
                Datastructures.Graphs.Relation.AktualnostType aktualnost)>
            _holdingDotaceCache = Devmasters.Cache.Couchbase.Manager<StatisticsSubjectPerYear<Firma.Statistics.Dotace>, (Firma firma,
                    Datastructures.Graphs.Relation.AktualnostType aktualnost)>
                .GetSafeInstance("Holding_DotaceStatistics_v3",
                    (obj) => CalculateHoldingDotaceStat(obj.firma, obj.aktualnost),
                    TimeSpan.FromDays(7),
                    Devmasters.Config.GetWebConfigValue("CouchbaseServers").Split(','),
                    Devmasters.Config.GetWebConfigValue("CouchbaseBucket"),
                    Devmasters.Config.GetWebConfigValue("CouchbaseUsername"),
                    Devmasters.Config.GetWebConfigValue("CouchbasePassword"),
                    obj => obj.firma.ICO + "-" + obj.aktualnost.ToString());

        public static StatisticsSubjectPerYear<Firma.Statistics.Dotace> CachedHoldingStatisticsDotace(
            Firma firma, Datastructures.Graphs.Relation.AktualnostType aktualnost,
            bool forceUpdateCache = false)
        {
            if (forceUpdateCache)
                _holdingDotaceCache.Delete((firma, aktualnost));

            return _holdingDotaceCache.Get((firma, aktualnost));
        }

        public static StatisticsSubjectPerYear<Firma.Statistics.Dotace> CachedStatisticsDotace(
            Firma firma,
            bool forceUpdateCache = false)
        {
            if (forceUpdateCache)
                _dotaceCache.Delete(firma);

            return _dotaceCache.Get(firma);
        }

        private static StatisticsSubjectPerYear<Firma.Statistics.Dotace> CalculateHoldingDotaceStat(Firma firma,
            Datastructures.Graphs.Relation.AktualnostType aktualnost)
        {
            var statistiky = firma.Holding(aktualnost)
                .Append(firma)
                .Where(f => f.Valid)
                .Select(f => new { f.ICO, dotaceStats = f.StatistikaDotaci() });

            var statistikyPerIco = new Dictionary<string, StatisticsSubjectPerYear<Firma.Statistics.Dotace>>();
            foreach (var ico in statistiky.Select(m=>m.ICO).Distinct())
            {
                statistikyPerIco[ico] = new StatisticsSubjectPerYear<Firma.Statistics.Dotace>();
                statistikyPerIco[ico] = StatisticsSubjectPerYear<Firma.Statistics.Dotace>.Aggregate(statistikyPerIco.Where(w => w.Key == ico).Select(m=>m.Value));
            }

            var aggregate = Lib.Analytics.StatisticsSubjectPerYear<Firma.Statistics.Dotace>.Aggregate(statistikyPerIco.Values);

            foreach (var year in aggregate)
            {
                year.Value.JednotliveFirmy = statistikyPerIco
                    .Where(s => s.Value.StatisticsForYear(year.Year).CelkemCerpano != 0)
                    .ToDictionary(s => s.Key, s => s.Value.StatisticsForYear(year.Year).CelkemCerpano);
            }

            return aggregate;
        }

        private static async Task<StatisticsSubjectPerYear<Firma.Statistics.Dotace>> CalculateDotaceStatAsync(Firma f)
        {
            var dotaceFirmy = await DotaceRepo.GetDotaceForIcoAsync(f.ICO).ToListAsync();

            // doplnit počty dotací
            var statistiky = dotaceFirmy.GroupBy(d => d.DatumPodpisu?.Year)
                .ToDictionary(g => g.Key ?? 0,
                    g => new Firma.Statistics.Dotace()
                    {
                        PocetDotaci = g.Count()
                    }
                );

            var cerpani = dotaceFirmy
                .SelectMany(d => d.Rozhodnuti)
                .SelectMany(r => r.Cerpani);

            var dataYearly = cerpani
                .GroupBy(c => c.GuessedYear)
                .ToDictionary(g => g.Key ?? 0,
                    g => (CelkemCerpano: g.Sum(c => c.CastkaSpotrebovana ?? 0),
                        PocetCerpani: g.Count(c => c.CastkaSpotrebovana.HasValue))
                );

            foreach (var dy in dataYearly)
            {
                if (!statistiky.TryGetValue(dy.Key, out var yearstat))
                {
                    yearstat = new Firma.Statistics.Dotace();
                    statistiky.Add(dy.Key, yearstat);
                }

                yearstat.CelkemCerpano = dy.Value.CelkemCerpano;
                yearstat.PocetCerpani = dy.Value.PocetCerpani;
            }


            return new StatisticsSubjectPerYear<Firma.Statistics.Dotace>(f.ICO, statistiky);
        }
    }
}