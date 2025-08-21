using System.IO;

namespace Game.NetworkContracts
{
    public interface IPacket
    {
        PacketId PacketId { get; }

        void Serialize(BinaryWriter writer);
        void Deserialize(BinaryReader reader);
    }
}
