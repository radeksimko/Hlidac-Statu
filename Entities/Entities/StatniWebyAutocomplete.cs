﻿using System;

namespace HlidacStatu.Entities
{
    public class StatniWebyAutocomplete : IEquatable<StatniWebyAutocomplete>
    {
        public int Id { get; set; }
        [FullTextSearch.Search]
        public string Name { get; set; }
        [FullTextSearch.Search]
        public string Description { get; set; }
        [FullTextSearch.Search]
        public string Ico { get; set; }
        [FullTextSearch.Search]
        public string Url { get; set; }
        public string HostDomain { get; set; }

        public StatniWebyAutocomplete(UptimeServer uptimeServer)
        {
            Id = uptimeServer.Id;
            Name = uptimeServer.Name;
            Description = uptimeServer.Description;
            Ico = uptimeServer.ICO;
            Url = uptimeServer.PublicUrl;
            HostDomain = uptimeServer.HostDomain();

        }
        
        public bool Equals(StatniWebyAutocomplete other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((StatniWebyAutocomplete)obj);
        }

        public override int GetHashCode()
        {
            return Id;
        }

   
    }


}
