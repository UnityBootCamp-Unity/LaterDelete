using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Game.NetworkContracts
{
    /// <summary>
    /// Rpc 함수 모음
    /// </summary>
    public static class RpcRegistry
    {
        private static readonly Dictionary<uint, MethodInfo> s_methodsOnClient = new Dictionary<uint, MethodInfo>();
        private static readonly Dictionary<uint, MethodInfo> s_methodsOnServer = new Dictionary<uint, MethodInfo>();

        /// <summary> 
        /// 지정 어셈블리들에서 [Rpc] Attribute 가 붙은 함수들을 모두 검색하여 테이블에 등록해놓음.
        /// </summary>
        public static void Collect(params Assembly[] assemblies)
        {
            foreach (Assembly assembly in assemblies)
            {
                IEnumerable<MethodInfo> filtered = assembly.GetTypes()
                                                           .SelectMany(type => type.GetMethods(BindingFlags.Public |
                                                                                               BindingFlags.Static |
                                                                                               BindingFlags.Instance));

                foreach (MethodInfo method in filtered)
                {
                    RpcImplementationAttribute rpcAttribute = method.GetCustomAttribute<RpcImplementationAttribute>();

                    if (rpcAttribute == null)
                        continue;

                    switch (rpcAttribute.Target)
                    {
                        case RpcImplementationTarget.Server:
                            s_methodsOnServer[rpcAttribute.Id] = method;
                            break;
                        case RpcImplementationTarget.Client:
                            s_methodsOnClient[rpcAttribute.Id] = method;
                            break;
                        default:
                            throw new NotImplementedException($"{rpcAttribute.Target} is not supported yet.");
                    }
                }
            }
        }

        public static bool TryGetClientRpc(uint rpcId, out MethodInfo method)
        {
            return s_methodsOnClient.TryGetValue(rpcId, out method);
        }

        public static bool TryGetServerRpc(uint rpcId, out MethodInfo method)
        {
            return s_methodsOnServer.TryGetValue(rpcId, out method);
        }
    }
}
