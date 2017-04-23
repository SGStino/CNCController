using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class DatagramViewModel
    {
        private const int Width = 16;
        public DatagramViewModel(byte[] data)
        {
            int count = (data.Length + Width - 1) / Width;

            partitions = new DatagramPartitionViewModel[count];

            for (int i = 0; i < count; ++i)
            {
                var start = i * Width;
                partitions[i] = new DatagramPartitionViewModel(data.Skip(start).Take(Width).ToArray());
            }
        }

        private DatagramPartitionViewModel[] partitions;
        public IEnumerable<DatagramPartitionViewModel> Partitions => partitions;
    }
    public class DatagramPartitionViewModel
    {
        public DatagramPartitionViewModel(byte[] data)
        {
            this.Data = data;
        }

        public byte[] Data { get; }

        public string String => new string(getChars(Data).ToArray());

        private IEnumerable<char> getChars(byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                yield return getChar(data[i]);
            }
        }

        private char getChar(byte arg)
        {
            char chr = (char)arg;

            if (char.IsLetterOrDigit(chr))
                return chr;
            return ' ';
        }
    }


}
