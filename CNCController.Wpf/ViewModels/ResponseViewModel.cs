using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CNCController.Wpf.ViewModels
{
    public class ResponseViewModel
    {
        private Response response;

        public ResponseViewModel(Response response)
        {
            this.response = response;
        }

        public ResponseType Type => response.Type;
        public int QueueLength => response.QueueLength;
        public int QueueAvailable => response.QueueAvailable;
        public int TotalQueue => QueueLength + QueueAvailable;
        public MessageType Command => response.Header.Type;
        public ulong Id => response.Header.Id;
    }
}
