using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Podcasts
{
    public class SearchResponseEntry
    {
        public string artistName { get; set; }
        public string trackName { get; set; }
        public string feedUrl { get; set; }
        public string artworkUrl100 { get; set; }
        public string artworkUrl600 { get; set; }
        public int trackCount { get; set; }

        public string[] genres { get; set; }

        public string GenresAsString => string.Join(", ", genres);
    }

    public class SearchResponse
    {
        public SearchResponseEntry[] results { get; set; }
    }
}
