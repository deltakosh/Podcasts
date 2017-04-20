using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Podcasts
{
    public class NavigationStack
    {
        public List<StackData> StatesStack
        {
            get;
            set;
        }

        public NavigationStack()
        {
            StatesStack = new List<StackData>();
        }

        public void Clear()
        {
            StatesStack.Clear();
        }

        public void Pop()
        {
            StatesStack.Remove(StatesStack.Last());
        }

        public async Task SerializeAsync()
        {
            var localFile = await FileHelper.CreateLocalFileAsync("callstack.data", false);

            using (var fileStream = await localFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                using (var writer = new DataWriter(fileStream))
                {
                    writer.WriteInt32(StatesStack.Count);
                    foreach (var stackData in StatesStack)
                    {
                        stackData.Serialize(writer);
                    }

                    await writer.StoreAsync();
                    await writer.FlushAsync();
                }
            }
        }

        public static async Task<NavigationStack> DeserializeAsync()
        {
            NavigationStack result = new NavigationStack();
            if (!await FileHelper.IsLocalFileExistsAsync("callstack.data", false))
            {
                return result;
            }
            var localFile = await FileHelper.GetLocalFileAsync("callstack.data", false);
            using (var fileStream = await localFile.OpenReadAsync())
            {
                using (var reader = new DataReader(fileStream))
                {
                    reader.InputStreamOptions = InputStreamOptions.ReadAhead;

                    await reader.LoadAsync((uint)fileStream.Size);

                    var stacksCount = reader.ReadInt32();
                    for (var index = 0; index < stacksCount; index++)
                    {
                        result.StatesStack.Add(StackData.Deserialize(reader));
                    }
                }
            }

            return result;
        }
    }
}
