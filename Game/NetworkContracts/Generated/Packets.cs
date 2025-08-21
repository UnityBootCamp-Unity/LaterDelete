using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Game.NetworkContracts
{
    public sealed class S_ConnectionSuccess : IPacket
    {
        public PacketId PacketId => PacketId.S_ConnectionSuccess;

        public int AssignedClientId { get; set; }
        public string Message { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(AssignedClientId);
            writer.Write(Message);
        }

        public void Deserialize(BinaryReader reader)
        {
            AssignedClientId = reader.ReadInt32();
            Message = reader.ReadString();
        }
    }

    public sealed class S_ConnectionFailure : IPacket
    {
        public PacketId PacketId => PacketId.S_ConnectionFailure;

        public string Reason { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Reason);
        }

        public void Deserialize(BinaryReader reader)
        {
            Reason = reader.ReadString();
        }
    }

    public sealed class C_Ping : IPacket
    {
        public PacketId PacketId => PacketId.C_Ping;

        public long ClientTicks { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ClientTicks);
        }

        public void Deserialize(BinaryReader reader)
        {
            ClientTicks = reader.ReadInt64();
        }
    }

    public sealed class S_Pong : IPacket
    {
        public PacketId PacketId => PacketId.S_Pong;

        public long EchoTicks { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(EchoTicks);
        }

        public void Deserialize(BinaryReader reader)
        {
            EchoTicks = reader.ReadInt64();
        }
    }

    public sealed class C_Login : IPacket
    {
        public PacketId PacketId => PacketId.C_Login;

        public string Id { get; set; }
        public string Pw { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Pw);
        }

        public void Deserialize(BinaryReader reader)
        {
            Id = reader.ReadString();
            Pw = reader.ReadString();
        }
    }

    public sealed class S_LoginResult : IPacket
    {
        public PacketId PacketId => PacketId.S_LoginResult;

        public bool Success { get; set; }
        public int AssignedClientId { get; set; }
        public string Message { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Success);
            writer.Write(AssignedClientId);
            writer.Write(Message);
        }

        public void Deserialize(BinaryReader reader)
        {
            Success = reader.ReadBoolean();
            AssignedClientId = reader.ReadInt32();
            Message = reader.ReadString();
        }
    }

    public sealed class S_ChatSend : IPacket
    {
        public PacketId PacketId => PacketId.S_ChatSend;

        public int SenderId { get; set; }
        public string Text { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(SenderId);
            writer.Write(Text);
        }

        public void Deserialize(BinaryReader reader)
        {
            SenderId = reader.ReadInt32();
            Text = reader.ReadString();
        }
    }

    public sealed class C_ChatSend : IPacket
    {
        public PacketId PacketId => PacketId.C_ChatSend;

        public string Text { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(Text);
        }

        public void Deserialize(BinaryReader reader)
        {
            Text = reader.ReadString();
        }
    }

    public sealed class ClientRpcRequest : IPacket
    {
        public PacketId PacketId => PacketId.ClientRpcRequest;

        public uint RpcId { get; set; }
        public string JsonData { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(RpcId);
            writer.Write(JsonData);
        }

        public void Deserialize(BinaryReader reader)
        {
            RpcId = reader.ReadUInt32();
            JsonData = reader.ReadString();
        }
    }

    public sealed class ClientRpcReponse : IPacket
    {
        public PacketId PacketId => PacketId.ClientRpcReponse;

        public uint RpcId { get; set; }
        public string JsonData { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(RpcId);
            writer.Write(JsonData);
        }

        public void Deserialize(BinaryReader reader)
        {
            RpcId = reader.ReadUInt32();
            JsonData = reader.ReadString();
        }
    }

    public sealed class ServerRpcRequest : IPacket
    {
        public PacketId PacketId => PacketId.ServerRpcRequest;

        public uint RpcId { get; set; }
        public string JsonData { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(RpcId);
            writer.Write(JsonData);
        }

        public void Deserialize(BinaryReader reader)
        {
            RpcId = reader.ReadUInt32();
            JsonData = reader.ReadString();
        }
    }

    public sealed class ServerRpcReponse : IPacket
    {
        public PacketId PacketId => PacketId.ServerRpcReponse;

        public uint RpcId { get; set; }
        public string JsonData { get; set; }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(RpcId);
            writer.Write(JsonData);
        }

        public void Deserialize(BinaryReader reader)
        {
            RpcId = reader.ReadUInt32();
            JsonData = reader.ReadString();
        }
    }
}