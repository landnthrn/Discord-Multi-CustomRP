using DiscordRPC;
using System;
using System.Reflection;

namespace CustomRPC
{
    /// <summary>
    /// Overrides DiscordRpcClient.ProcessID before Initialize (Multi-RP fake PID).
    /// </summary>
    public static class DiscordRpcProcessIdHelper
    {
        const int FakePidBase = 48000;

        public static int AllocateFakeProcessId(string slotId, int fallbackIndex)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in slotId ?? "")
                    hash = hash * 31 + c;

                int offset = Math.Abs(hash % 40000);
                return FakePidBase + offset + ((fallbackIndex + 1) * 37);
            }
        }

        public static bool TrySetProcessId(DiscordRpcClient client, int processId)
        {
            if (client == null || processId <= 0)
                return false;

            try
            {
                PropertyInfo property = typeof(DiscordRpcClient).GetProperty(
                    "ProcessID",
                    BindingFlags.Instance | BindingFlags.Public);

                if (property == null || !property.CanWrite)
                    return false;

                property.SetValue(client, processId, null);
                return (int)property.GetValue(client, null) == processId;
            }
            catch
            {
                return false;
            }
        }
    }
}
