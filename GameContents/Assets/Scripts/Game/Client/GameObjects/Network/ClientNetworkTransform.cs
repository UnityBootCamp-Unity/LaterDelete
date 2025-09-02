using Unity.Netcode.Components;
using UnityEngine;

namespace Game.Client.GameObjects.Network
{
    public class ClientNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative()
        {
            return false;
        }
    }
}

// 실시간 동기화 Network Variable
// 이벤트성 동기화가 필요하면 RPC