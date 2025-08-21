using Game.NetworkContracts;
using System.Reflection;
using System.Text;

namespace PacketTool
{
    internal static class TypeBuilderForRpc
    {
        internal static void Build(string clientAssemblyPath,
                                   string serverAssemblyPath,
                                   string rpcIdsOutDir,
                                   string clientRpcProxyOutDir,
                                   string serverRpcProxyOutDir)
        {
            Assembly client = LoadOrNull(clientAssemblyPath);
            Assembly server = LoadOrNull(serverAssemblyPath);
            List<MethodInfo> clientImplMethods;
            List<MethodInfo> serverImplMethods;
            List<MethodInfo> allImplMethods = new List<MethodInfo>();

            if (client != null)
            {
                clientImplMethods = CollectRpcMethods(client, RpcImplementationTarget.Client);
                allImplMethods.AddRange(clientImplMethods);
                Console.WriteLine($"Found Client Impl Rpc methods total {clientImplMethods.Count}");
            }
            else
            {
                clientImplMethods = new List<MethodInfo>();
                Console.WriteLine($"Not Found Client Impl Rpc methods");
            }

            if (server != null)
            {
                serverImplMethods = CollectRpcMethods(server, RpcImplementationTarget.Server);
                allImplMethods.AddRange(serverImplMethods);
                Console.WriteLine($"Found Server Impl Rpc methods total {serverImplMethods.Count}");
            }
            else
            {
                serverImplMethods = new List<MethodInfo>();
                Console.WriteLine($"Not Found Server Impl Rpc methods");
            }

            // Generate rpc id 
            RpcIdGenerator rpcIdGenerator = new RpcIdGenerator();
            rpcIdGenerator.Generate(allImplMethods);
            BuildRpcIdsClass(rpcIdGenerator.IdToSign, rpcIdsOutDir);

            BuildClientRpcProxyClass(clientRpcProxyOutDir, clientImplMethods);   // 서버에서 사용하는 프록시
            BuildServerRpcProxyClass(serverRpcProxyOutDir, serverImplMethods);   // 클라이언트에서 사용하는 프록시
        }

        private static Assembly LoadOrNull(string path)
        {
            try { return File.Exists(path) ? Assembly.LoadFrom(path) : null; }
            catch { return null; }
        }

        private static List<MethodInfo> CollectRpcMethods(Assembly assembly, RpcImplementationTarget implTarget)
        {
            IEnumerable<MethodInfo> filtered = SafeGetTypes(assembly)
                                                       .SelectMany(type => type.GetMethods(BindingFlags.Public |
                                                                                           BindingFlags.Static |
                                                                                           BindingFlags.Instance));

            List<MethodInfo> result = new List<MethodInfo>();

            foreach (MethodInfo method in filtered)
            {
                RpcImplementationAttribute rpcImplAttribute = method.GetCustomAttribute<RpcImplementationAttribute>();

                if (rpcImplAttribute == null)
                    continue;

                if (rpcImplAttribute.Target == implTarget)
                    result.Add(method);
            }

            return result;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try
            {
                return asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // 못 불러온 타입은 null 로 채워져 있으므로 걸러낸다
                return ex.Types.Where(t => t != null);
            }

        }
        private static void BuildRpcIdsClass(IReadOnlyDictionary<uint, string> table, string outDir)
        {
            StringBuilder sb = new StringBuilder("/* Auto-generated */\n");
            sb.AppendLine("""
                using System;
                using System.Collections.Generic;

                namespace Game.Core.Network
                {
                    public static class RpcIds
                    {
                """);


            sb.AppendLine("""
                        private static readonly Dictionary<uint, string> _idToSign = new Dictionary<uint, string>
                        {
                """);

            foreach (var pair in table)
            {
                sb.Append($"            ");
                sb.AppendLine($"[{pair.Key}] = \"{pair.Value}\",");
            }

            sb.Append("""
                        };
                    }
                }
                """);

            File.WriteAllText(Path.Combine(outDir, "RpcIds.g.cs"), sb.ToString(), Encoding.UTF8);
        }


        /// <summary>
        /// For server
        /// </summary>
        private static void BuildClientRpcProxyClass(string outDir, IEnumerable<MethodInfo> clientImplRpcMethods)
        {
            var path = Path.Combine(outDir, "ClientRpcProxy.g.cs");

            // *** 디버깅 로그 ***
            Console.WriteLine($"[RpcTool]   › ClientProxy → {path}");
            Console.WriteLine($"[RpcTool]   › methods     = {clientImplRpcMethods.Count()}");

            StringBuilder sb = new StringBuilder("/* Auto generated */\n");
            sb.AppendLine("""
                using Newtonsoft.Json;

                namespace Game.Server.Network
                {
                    public partial class ClientRpcProxy
                    {
                """);

            foreach (MethodInfo method in clientImplRpcMethods)
            {
                sb.Append($"        ");
                sb.Append($"public {TypeLookups.NameByType[method.ReturnType]} {method.Name}");
                sb.Append('(');

                // 서버가 대상 클라이언트를 지정하기 위한 clientId 매개변수
                sb.Append("int clientId");

                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length > 0)
                    sb.Append(", ");

                for (int i = 0; i < parameters.Length; i++)
                {
                    string typeStr = TypeLookups.NameByType[parameters[i].ParameterType];
                    string nameStr = parameters[i].Name;
                    sb.Append($"{typeStr} {nameStr}");

                    if (i < parameters.Length - 1)
                        sb.Append(", ");
                }

                sb.Append(')');
                sb.AppendLine();
                // 구현부
                sb.AppendLine("        {");

                sb.Append($"            ");
                sb.Append("object[] args = new object[] { ");

                for (int i = 0; i < parameters.Length; i++)
                {
                    sb.Append($"{parameters[i].Name}");

                    if (i < parameters.Length - 1)
                        sb.Append(", ");
                }

                sb.AppendLine(" };");
                sb.Append($"            ");
                sb.AppendLine("string jsonData = JsonConvert.SerializeObject(args);");
                sb.Append($"            ");
                var rpcImpleAttribute = method.GetCustomAttribute<RpcImplementationAttribute>();
                string rpcId = rpcImpleAttribute.Id.ToString();
                sb.AppendLine($"SendRpc(clientId, {rpcId}, jsonData);");

                sb.AppendLine("        }");
            }

            sb.AppendLine("    }\n}");
            Directory.CreateDirectory(outDir);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Console.WriteLine("Generated ClientRpcProxy.g.cs");
        }
        /// <summary>
        /// For **client**―서버로 호출을 보내기 위한 프록시
        /// </summary>
        private static void BuildServerRpcProxyClass(string outDir, IEnumerable<MethodInfo> serverImplRpcMethods)
        {
            var path = Path.Combine(outDir, "ServerRpcProxy.g.cs");

            // *** 디버깅 로그 ***
            Console.WriteLine($"[RpcTool]   › ServerProxy → {path}");
            Console.WriteLine($"[RpcTool]   › methods     = {serverImplRpcMethods.Count()}");

            var sb = new StringBuilder("/* Auto generated */\n");
            sb.AppendLine("""
                using Newtonsoft.Json;

                namespace Game.Client.Network
                {
                    public partial class ServerRpcProxy
                    {
                """);

            foreach (var method in serverImplRpcMethods)
            {
                sb.Append("        public ");
                sb.Append(TypeLookups.NameByType[method.ReturnType]);
                sb.Append(' ');
                sb.Append(method.Name);
                sb.Append('(');

                var ps = method.GetParameters();
                for (int i = 0; i < ps.Length; i++)
                {
                    sb.Append($"{TypeLookups.NameByType[ps[i].ParameterType]} {ps[i].Name}");
                    if (i < ps.Length - 1) sb.Append(", ");
                }
                sb.AppendLine(")");

                sb.AppendLine("        {");
                sb.Append("            object[] args = { ");
                sb.Append(string.Join(", ", ps.Select(p => p.Name)));
                sb.AppendLine(" };");

                var rpcAttr = method.GetCustomAttribute<RpcImplementationAttribute>();
                sb.AppendLine($"            string json = JsonConvert.SerializeObject(args);");
                sb.AppendLine($"            SendRpc({rpcAttr.Id}, json);");
                sb.AppendLine("        }");
            }

            sb.AppendLine("    }\n}");

            Directory.CreateDirectory(outDir);
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Console.WriteLine("Generated ServerRpcProxy.g.cs");
        }
    }
}
