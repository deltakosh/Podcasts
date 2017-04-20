using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Podcasts
{
    public static class LocalLog
    {
        public static async Task WriteEntryAsync(string message)
        {
            var file = await FileHelper.GetOrCreateLocalFileAsync("log.txt", false);
            await FileIO.AppendTextAsync(file, $"{DateTime.Now}: {message}\r\n");
        }

        public static async Task<string> GetLogContent()
        {
            var file = await FileHelper.GetLocalFileAsync("log.txt", false);
            return await FileIO.ReadTextAsync(file);
        }
    }
}
