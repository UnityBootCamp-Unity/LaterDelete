using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Game.Client.GameObjects.Network
{
    public class Pickable : NetworkBehaviour
    {
        public NetworkVariable<ulong> pickerClientId = new NetworkVariable<ulong>(
                value: 0,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        public NetworkVariable<ulong> pickerObjectId = new NetworkVariable<ulong>(
                value: 0,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Server
            );

        Rigidbody _rigidbody;
        Collider _collider;
        GameObject _rendererRoot;
        NetworkTransform _networkTransform;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _networkTransform = GetComponent<NetworkTransform>();
            _rendererRoot = transform.Find("Renderer").gameObject;
        }

        public bool TryPickedUpBy(ulong pickerClientId, ulong pickerObjectId)
        {
            if (this.pickerClientId.Value > 0)
                return false;

            Debug.Log($"[{nameof(Pickable)}] Client {pickerClientId}'s player {pickerObjectId} can pick {NetworkObjectId}");
            this.pickerClientId.Value = pickerClientId;
            this.pickerObjectId.Value = pickerObjectId;
            SetPhysicsEnabled(false);
            AttachRendererToHandOfPickerClientRpc(pickerObjectId);
            return true;
        }

        public bool TryDroppedBy(ulong pickerClientId)
        {
            if (this.pickerClientId.Value != pickerClientId)
                throw new System.Exception($"{pickerClientId} cannot drop {NetworkObjectId}. because it's not the picker.");

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickerObjectId.Value, out NetworkObject networkObject))
            {
                if (networkObject.TryGetComponent(out PlayerController pc))
                {
                    _networkTransform.Teleport(pc.rightHand.position, pc.rightHand.rotation, Vector3.one);
                    DetachRendererFromHandOfPickerClientRpc(pickerObjectId.Value);
                    this.pickerClientId.Value = 0;
                    this.pickerObjectId.Value = 0;
                    SetPhysicsEnabled(true);
                    return true;
                }
            }

            return false;
        }

        public bool TryThrownBy(ulong pickerClientId, Vector3 impulse)
        {
            if (this.pickerClientId.Value != pickerClientId)
                throw new System.Exception($"{pickerClientId} cannot throw {NetworkObjectId}. because it's not the picker.");

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickerObjectId.Value, out NetworkObject networkObject))
            {
                if (networkObject.TryGetComponent(out PlayerController pc))
                {
                    _networkTransform.Teleport(pc.rightHand.position, pc.rightHand.rotation, Vector3.one);
                    DetachRendererFromHandOfPickerClientRpc(pickerObjectId.Value);
                    this.pickerClientId.Value = 0;
                    this.pickerObjectId.Value = 0;
                    SetPhysicsEnabled(true);
                    _rigidbody.AddForce(impulse, ForceMode.Impulse);
                    _rigidbody.AddTorque(Vector3.one, ForceMode.Impulse);
                    return true;
                }
            }

            return false;
        }

        private void SetPhysicsEnabled(bool enabled)
        {
            _rigidbody.useGravity = enabled;
            _collider.enabled = enabled;

            if (!enabled)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        [Rpc(SendTo.NotServer)]
        private void AttachRendererToHandOfPickerClientRpc(ulong pickerObjectId)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickerObjectId, out NetworkObject networkObject))
            {
                if (networkObject.TryGetComponent(out PlayerController pc))
                {
                    _rendererRoot.transform.SetParent(pc.rightHand);
                    _rendererRoot.transform.localPosition = Vector3.zero;
                    _rendererRoot.transform.localRotation = Quaternion.identity;
                    _collider.enabled = false;
                }
            }
            else
            {
                // TODO : 플레이어 캐릭터 사라짐 예외
            }
        }

        [Rpc(SendTo.NotServer)]
        private void DetachRendererFromHandOfPickerClientRpc(ulong pickerObjectId)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickerObjectId, out NetworkObject networkObject))
            {
                if (networkObject.TryGetComponent(out PlayerController pc))
                {
                    _rendererRoot.transform.SetParent(transform);
                    _rendererRoot.transform.localPosition = Vector3.zero;
                    _rendererRoot.transform.localRotation = Quaternion.identity;
                    _collider.enabled = true;
                }
            }
            else
            {
                // TODO : 플레이어 캐릭터 사라짐 예외
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // TODO
            // 마지막으로 던진 ClientId 캐싱해놓고
            // 그 ClientId 외에 다른 Client의 PlayerController 와 충돌했다면 체력깎는 등의 컨텐츠...
        }
    }
}