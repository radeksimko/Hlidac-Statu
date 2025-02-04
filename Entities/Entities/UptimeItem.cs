﻿using System;

namespace HlidacStatu.Entities
{
    public partial class UptimeItem
    {

        public const decimal TimeOut = 33.333m;

        [Nest.Keyword]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        [Nest.Keyword]
        public int ServerId { get; set; }

        [Nest.Keyword]
        public string Url { get; set; }

        [Nest.Date]
        public DateTime CheckStart { get; set; }

        [Nest.Date]
        public DateTime CheckEnd { get; set; }

        [Nest.Number]
        public decimal ResponseCode { get; set; }

        [Nest.Number]
        public long ResponseTimeInMs { get; set; }

        [Nest.Number]
        public long ResponseSize { get; set; }


        [Nest.Boolean]
        public bool ScreenshotTriggered { get; set; }

        [Nest.Keyword]
        public string Uptimer { get; set; }

        public UptimeServer.Availability ToAvailability()
        {
            UptimeServer.Availability av = new UptimeServer.Availability();
            av.Time = this.CheckStart;
            av.HttpStatusCode = (int) this.ResponseCode;
            av.ResponseTimeInSec = this.ResponseTimeInMs/1000m; //convert To Sec
            return av;

        }

    }
}
