using Windows.Storage.Streams;

namespace Podcasts
{
    public class StackData
    {
        public string Data { get; set; }
        public string FiltersData { get; set; }
        public int ID { get; set; }
        public string ScrollPosition { get; set; }
        public int[] IDs { get; set; }
        public int MenuIndex { get; set; }

        public void Serialize(DataWriter writer)
        {
            writer.WriteAdvancedString(Data);
            writer.WriteAdvancedString(FiltersData);
            writer.WriteAdvancedString(ScrollPosition);
            writer.WriteInt32(ID);
            writer.WriteInt32(MenuIndex);

            if (IDs == null)
            {
                writer.WriteInt32(0);
                return;
            }
            writer.WriteInt32(IDs.Length);
            for (var index = 0; index < IDs.Length; index++)
            {
                writer.WriteInt32(IDs[index]);
            }
        }

        public static StackData Deserialize(DataReader reader)
        {
            var stackData = new StackData();

            stackData.Data = reader.ReadString();
            stackData.FiltersData = reader.ReadString();
            stackData.ScrollPosition = reader.ReadString();

            stackData.ID = reader.ReadInt32();
            stackData.MenuIndex = reader.ReadInt32();

            var idsLength = reader.ReadInt32();

            if (idsLength > 0)
            {
                stackData.IDs = new int[idsLength];
                for (var index = 0; index < idsLength; index++)
                {
                    stackData.IDs[index] = reader.ReadInt32();
                }
            }

            return stackData;
        }
    }
}
