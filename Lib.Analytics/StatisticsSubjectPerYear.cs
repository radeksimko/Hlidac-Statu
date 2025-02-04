﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace HlidacStatu.Lib.Analytics
{
    // !!!!! Michale, za žádnou cenu sem nedávej ToNiceString !!!! 
    // Nebo začne bůh topit koťátka, dokud ti to nesmažu! :-D

    public class StatisticsSubjectPerYear<T> : StatisticsPerYear<T>
        where T : CoreStat, IAddable<T>, new()
    {
        public string ICO { get; set; }


        public StatisticsSubjectPerYear()
        : base()
        { }

        public StatisticsSubjectPerYear(string ico, StatisticsPerYear<T> baseObj)
            : base(baseObj)
        {
            ICO = ico;
        }

        public StatisticsSubjectPerYear(string ico, Func<T, int> yearSelector, IEnumerable<T> data)
            : base(yearSelector, data)
        {
            ICO = ico;
        }

        /// <summary>
        /// Creates new statistics
        /// </summary>
        /// <param name="ico">Subject Ico</param>
        /// <param name="data">Dictionary where key = Year, value = T</param>
        public StatisticsSubjectPerYear(string ico, Dictionary<int, T> data)
            : base(data)
        {
            ICO = ico;
        }


        public static StatisticsSubjectPerYear<T> Aggregate(IEnumerable<StatisticsSubjectPerYear<T>> statistics)
        {
            if (statistics is null)
                return new StatisticsSubjectPerYear<T>();

            var aggregatedStatistics = new StatisticsSubjectPerYear<T>(
                $"aggregated for {statistics.FirstOrDefault().ICO}",
                AggregateStats(statistics));

            return aggregatedStatistics;
        }

    }
}
