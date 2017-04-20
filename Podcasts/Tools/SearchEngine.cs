using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Podcasts
{
    public static class SearchEngine
    {
        public static async Task<SearchResponseEntry[]> SearchAsync(string keywords, string country = "US", int limit = 50)
        {
            keywords = keywords.Replace(" ", "+");

            var responseString = await CoreTools.DownloadStringAsync($"https://itunes.apple.com/search?term={keywords}&media=podcast&country={country}&limit={limit}");

            var response = JsonConvert.DeserializeObject<SearchResponse>(responseString);

            return response.results;
        }
    }
}
