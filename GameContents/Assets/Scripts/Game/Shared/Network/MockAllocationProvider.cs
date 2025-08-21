namespace Game.Shared.Network
{
    public class MockAllocationProvider : IAllocationProvider
    {
        public bool hasAllocation => true;

        public string ipAddress => "0.0.0.0";

        public ushort port => 7777;
    }
}
