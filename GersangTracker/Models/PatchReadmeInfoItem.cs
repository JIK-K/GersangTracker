using System;
using System.Collections.Generic;

namespace GersangTracker.Models
{
    public class PatchReadmeInfoItem
    {
        public DateTime Date { get; }
        public int Version { get; }
        public IReadOnlyList<string> Details { get; }

        public PatchReadmeInfoItem(DateTime date, int version, IReadOnlyList<string> details)
        {
            Date = date;
            Version = version;
            Details = details ?? Array.Empty<string>();
        }

        public string Display => $"v{Version} ({Date:yyyy.MM.dd})";
    }
}