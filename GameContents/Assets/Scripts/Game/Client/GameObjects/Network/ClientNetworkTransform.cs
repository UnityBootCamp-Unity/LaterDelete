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