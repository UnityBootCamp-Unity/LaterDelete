using UnityEngine;

namespace Game.Client.Network
{
    [CreateAssetMenu(fileName = "ConnectionSettings", menuName = "Scriptable Objects/ConnectionSettings")]
    public class ConnectionSettings : ScriptableObject
    {
        [field: SerializeField] public string serverIp { get; private set; } = "127.0.0.1";
        [field: SerializeField] public int serverPort { get; private set; } = 7777;
    }
}