using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.PatchManager;

namespace CrunchEconContractModels.Random_Stuff
{
    [PatchShim]
    public static class NexusHelper
    {
        public static void Patch(PatchContext ctx)
        {
        }

        public static Action<byte[]> MessageRecieved;

        public static void SendToChat(string author, string message, string color)
        {

        }

        private static void SendToChatIngame()
        {

        }

        public static void SendToNexus(byte[] data)
        {

        }

        public static void ReceiveFromNexus()
        {

        }
    }
}
