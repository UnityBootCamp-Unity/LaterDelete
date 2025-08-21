using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared
{
    public class InGameManager : NetworkBehaviour
    {
        public enum State
        {
            WaitingForPlayers,
            CountdownToStart,
            Playing,
            Finished,
        }

        public NetworkVariable<State> state = new NetworkVariable<State>(
                value: State.WaitingForPlayers,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        private Dictionary<ulong, NetworkObject> _players = new Dictionary<ulong, NetworkObject>();

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[{nameof(InGameManager)}] spawned");
        }

        public void RegisterPlayer(ulong objectId)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject networkObject))
            {
                _players[networkObject.OwnerClientId] = networkObject;

                if (IsServer)
                {
                    CheckAllPlayerReady();
                }
            }
            else
            {
                // 플레이어 캐릭터 생성 안됨 예외
            }
        }

        private  void CheckAllPlayerReady()
        {
            int count = _players.Count;

            // TODO : Allocation 데이터를 Multiplay blackboard 등에서 읽어서 총 몇명이 준비되어야하는지 보고 비교한다음
            // 게임 상태 변경

            if (count == 4)
            {
                state.Value = State.CountdownToStart;
            }
        }
    }
}