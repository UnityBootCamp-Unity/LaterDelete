using UnityEngine;

namespace Game.Client.Network
{
    [CreateAssetMenu(fileName = "MultiplaySettings", menuName = "Scriptable Objects/MultiplaySettings")]
    public class MultiplaySettings : ScriptableObject
    {
        [field: SerializeField] public long buildConfigurationId = 1303249;
        [field: SerializeField] public string regionId = "8e2986cd-debc-4d2a-ac05-f54955912298";
    }
}
