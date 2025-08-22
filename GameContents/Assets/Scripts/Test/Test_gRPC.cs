using UnityEngine;

public class Test_gRPC : MonoBehaviour
{
    private async void Start()
    {
        Game.Client.Network.GrpcConnection.InitChannel();
        await Game.Client.Network.GrpcConnection.TestRegisterAsync();
    }
}
