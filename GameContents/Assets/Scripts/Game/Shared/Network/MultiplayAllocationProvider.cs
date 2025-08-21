#if UNITY_SERVER || ENABLE_UCS_SERVER
using Unity.Services.Multiplay;
#endif

using Unity.Services.Multiplayer;

namespace Game.Shared.Network
{
    public class MultiplayAllocationProvider : IAllocationProvider
    {
        public bool hasAllocation
        {
            get
            {
#if UNITY_SERVER || ENABLE_UCS_SERVER
                try
                {
                    var config = MultiplayService.Instance.ServerConfig;

                    if (config != null &&
                        config.Port > 0 &&
                        string.IsNullOrEmpty(config.AllocationId) == false)
                    {
                        return true;
                    }
                }
                catch
                {
                    return false;
                }
#endif
                return false;
            }
        }

        public string ipAddress => "0.0.0.0";

        public ushort port
        {
            get
            {
#if UNITY_SERVER || ENABLE_UCS_SERVER
                return MultiplayService.Instance.ServerConfig.Port;
#else
                return 0;
#endif
            }
        }
            
            
    }
}
