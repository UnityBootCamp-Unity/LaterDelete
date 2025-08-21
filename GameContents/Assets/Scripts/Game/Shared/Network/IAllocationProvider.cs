namespace Game.Shared.Network
{
    public interface IAllocationProvider
    {
        bool hasAllocation { get; }
        string ipAddress { get; }
        ushort port { get; }
    }
}
