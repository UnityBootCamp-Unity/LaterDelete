using PacketTool;

namespace RpcTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // 필요한 args :
            // 1. client 어셈블리
            // 2. server 어셈블리
            // 3. RpcIds 클래스 생성 디렉토리
            // 4. ClientRpcProxy 클래스 생성 디렉토리
            // 5. ServerRpcProxy 클래스 생성 디렉토리

            Console.WriteLine("Start Rpc generation");

            if (args.Length != 5)
            {
                Console.WriteLine("To build Rpc files, need must 2 paths and 3 out dirtories.");
                return;
            }

            #region Rpc files
            string clientAssemblyPath = args[0];
            string serverAssemblyPath = args[1];
            string rpcIdsOutDir = args[2];
            string clientRpcProxyOutDir = args[3];
            string serverRpcProxyOutDir = args[4];

            if (!File.Exists(clientAssemblyPath))
                Console.WriteLine($"Cannot find file {clientAssemblyPath}");
            
            if (!File.Exists(serverAssemblyPath))
                Console.WriteLine($"Cannot find file {serverAssemblyPath}");

            if (!Directory.Exists(rpcIdsOutDir))
                Directory.CreateDirectory(rpcIdsOutDir);

            if (!Directory.Exists(clientRpcProxyOutDir))
                Directory.CreateDirectory(clientRpcProxyOutDir);

            if (!Directory.Exists(serverRpcProxyOutDir))
                Directory.CreateDirectory(serverRpcProxyOutDir);

            TypeBuilderForRpc.Build(clientAssemblyPath, serverAssemblyPath, rpcIdsOutDir, clientRpcProxyOutDir, serverRpcProxyOutDir);
            #endregion
        }
    }
}
