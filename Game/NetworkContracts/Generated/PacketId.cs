using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Game.NetworkContracts
{    
    public enum PacketId : ushort    
    {    
        S_ConnectionSuccess = 0x0001,
        S_ConnectionFailure = 0x0002,
        C_Ping = 0x0005,
        S_Pong = 0x0006,
        C_Login = 0x0011,
        S_LoginResult = 0x0012,
        S_ChatSend = 0x1010,
        C_ChatSend = 0x1020,
        ClientRpcRequest = 0x2000,
        ClientRpcReponse = 0x2001,
        ServerRpcRequest = 0x2010,
        ServerRpcReponse = 0x2011,
    }
}
