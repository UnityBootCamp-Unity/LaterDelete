using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Game.NetworkContracts
{
    public enum RpcImplementationTarget
    {
        Server,
        Client,
    }

    [AttributeUsage(AttributeTargets.Method)] // Rpc 함수에만 붙여서 써야하므로 사용제한
    public class RpcImplementationAttribute : Attribute
    {
        public RpcImplementationAttribute(RpcImplementationTarget target)
        {
            Target = target;
        }


        public uint Id { get; set; }
        public RpcImplementationTarget Target { get; }
    }
}
