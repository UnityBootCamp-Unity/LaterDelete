using System;
using Game.Shared;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.GameObjects.Network
{
    [Flags]
    public enum Interactions
    {
        Idle        = 0 << 0,
        PickUp      = 1 << 0,
        Hold        = 1 << 1,
        Drop        = 1 << 2,
        Throw       = 1 << 3,
    }


    public class PlayerController : NetworkBehaviour
    {
        public Vector3 inputDirection { get; set; }

        [Header("Body parts")]
        [field:SerializeField] public Transform rightHand { get; private set; }
        [field:SerializeField] public Transform leftHand { get; private set; }

        [Header("Move")]
        [SerializeField] InputActionReference _moveAction;
        [SerializeField] float _moveSpeed = 3f;
        [SerializeField] float _rotateSpeed = 360f;

        [Header("PickUp")]
        [SerializeField] InputActionReference _pickUpAction;
        [SerializeField] LayerMask _pickableMask;
        [SerializeField] float _pickUpRange = 1.5f;
        public NetworkVariable<ulong> pickedUpObjectId = new NetworkVariable<ulong>(
                value: 0,
                readPerm: NetworkVariableReadPermission.Everyone,
                writePerm: NetworkVariableWritePermission.Owner
            );

        [Header("Throw")]
        [SerializeField] InputActionReference _throwAction;
        [SerializeField] float _throwImpulse = 20f;

        Interactions _interactions; // 현재 진행중인 상호작용
        Interactions _requiredToPickUp = Interactions.Idle; // Pick 을 하기위해서 선행되어야하는 상호작용
        Interactions _requiredToHold = Interactions.PickUp; // Hold 을 하기위해서 선행되어야하는 상호작용
        Interactions _requiredToDrop = Interactions.Hold;   // Drop 을 하기위해서 선행되어야하는 상호작용
        Interactions _requiredToThrow = Interactions.Hold;  // Throw 을 하기위해서 선행되어야하는 상호작용


        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _moveAction.action.performed += OnMovePerformed;
            _moveAction.action.canceled += OnMoveCanceled;
            _pickUpAction.action.started += OnPickUpStarted;
            _pickUpAction.action.canceled += OnPickUpCanceled;
            _throwAction.action.started += OnThrowStarted;

            RegisterToInGameManagerRpc(NetworkObjectId);
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            _moveAction.action.performed -= OnMovePerformed;
            _moveAction.action.canceled -= OnMoveCanceled;
            _pickUpAction.action.started -= OnPickUpStarted;
            _pickUpAction.action.canceled -= OnPickUpCanceled;
            _throwAction.action.started -= OnThrowStarted;
        }

        void OnMovePerformed(InputAction.CallbackContext context)
        {
            Vector2 value = context.ReadValue<Vector2>();
            inputDirection = new Vector3(value.x, 0f, value.y);
        }

        void OnMoveCanceled(InputAction.CallbackContext context)
        {
            inputDirection = Vector3.zero;
        }

        void OnPickUpStarted(InputAction.CallbackContext context)
        {
            TryPickUp();
        }

        void OnPickUpCanceled(InputAction.CallbackContext context)
        {
            TryDrop();
        }

        void OnThrowStarted(InputAction.CallbackContext context)
        {
            TryThrow();
        }

        private void FixedUpdate()
        {
            Move();
            SmoothRotate();
        }

        void Move()
        {
            Vector3 velocity = new Vector3(inputDirection.x, 0f, inputDirection.z).normalized * _moveSpeed;
            transform.Translate(velocity * Time.fixedDeltaTime, Space.World);
        }

        void SmoothRotate()
        {
            if (inputDirection.magnitude < 0.01f)
                return;

            Vector3 lookDir = new Vector3(inputDirection.x, 0f, inputDirection.z);
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotateSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime);
        }

        void TryPickUp()
        {
            // 픽업 조건 불충분
            if (_interactions != _requiredToPickUp)
                return;

            // 이미 들고있는게 있으면 무시
            if (pickedUpObjectId.Value > 0)
                return;

            Collider[] colliders = Physics.OverlapSphere(transform.position, _pickUpRange, _pickableMask);

            Pickable closest = null;
            float closestSqr = float.MaxValue;

            foreach (Collider collider in colliders)
            {
                float sqr = (transform.position - collider.transform.position).sqrMagnitude;

                if (sqr < closestSqr)
                {
                    closest = collider.GetComponent<Pickable>();
                    closestSqr = sqr;
                }
            }

            if (closest != null)
            {
                PickUpServerRpc(OwnerClientId, closest.GetComponent<NetworkObject>().NetworkObjectId);
                _interactions |= Interactions.PickUp;
            }
        }

        void TryDrop()
        {
            /*if (_interactions == Interactions.PickUp)
            {
                while (_interactions != _requiredToDrop)
                {
                    await Task.Delay(100);
                }
            }*/

            if (_interactions != _requiredToDrop)
                return;

            // 들고있는게 없으면 무시
            if (pickedUpObjectId.Value == 0)
                return;

            DropServerRpc(OwnerClientId);
            _interactions |= Interactions.Drop;
        }

        void TryThrow()
        {
            if (_interactions != _requiredToThrow)
                return;

            // 들고있는게 없으면 무시
            if (pickedUpObjectId.Value == 0)
                return;

            Vector3 impulse = inputDirection.sqrMagnitude > 0.1f ? inputDirection.normalized : transform.forward;
            impulse *= _throwImpulse;

            ThrowServerRpc(OwnerClientId, impulse);
            _interactions |= Interactions.Throw;
        }

        [Rpc(SendTo.Server)]
        void PickUpServerRpc(ulong clientId, ulong objectId)
        {
            Debug.Log($"[{nameof(PlayerController)}] Client {clientId} trying to pick up {objectId}");

            if (pickedUpObjectId.Value == 0 &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject networkObject))
            {
                Pickable pickable = networkObject.GetComponent<Pickable>();

                if (pickable.TryPickedUpBy(clientId, NetworkObjectId))
                {
                    PickedUpClientRpc(true, objectId);
                    return;
                }
            }

            PickedUpClientRpc(false, objectId);
            return;
        }

        [Rpc(SendTo.Owner)]
        void PickedUpClientRpc(bool success, ulong objectId)
        {
            if (success)
            {
                Debug.Log($"[{nameof(PlayerController)}] {NetworkObjectId} picked up {objectId}");
                pickedUpObjectId.Value = objectId;
                _interactions |= Interactions.Hold;
            }

            _interactions &= ~Interactions.PickUp;
        }

        [Rpc(SendTo.Server)]
        void DropServerRpc(ulong clientId)
        {
            Debug.Log($"[{nameof(PlayerController)}] Client {clientId} trying to drop {pickedUpObjectId.Value}");

            if (pickedUpObjectId.Value > 0 &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickedUpObjectId.Value, out NetworkObject networkObject))
            {
                Pickable pickable = networkObject.GetComponent<Pickable>();

                if (pickable.TryDroppedBy(clientId))
                {
                    DroppedClientRpc(true, pickedUpObjectId.Value);
                    return;
                }
            }

            DroppedClientRpc(false, pickedUpObjectId.Value);
        }

        [Rpc(SendTo.Owner)]
        void DroppedClientRpc(bool success, ulong objectId)
        {
            if (success)
            {
                Debug.Log($"[{nameof(PlayerController)}] {NetworkObjectId} dropped {objectId}");
                pickedUpObjectId.Value = 0;
            }

            _interactions &= ~(Interactions.Drop | Interactions.Hold);
        }

        [Rpc(SendTo.Server)]
        void ThrowServerRpc(ulong clientId, Vector3 impulse)
        {
            Debug.Log($"[{nameof(PlayerController)}] Client {clientId} trying to throw {pickedUpObjectId.Value}");

            if (pickedUpObjectId.Value > 0 &&
                NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(pickedUpObjectId.Value, out NetworkObject networkObject))
            {
                Pickable pickable = networkObject.GetComponent<Pickable>();

                if (pickable.TryThrownBy(clientId, impulse))
                {
                    ThrownClientRpc(true, pickedUpObjectId.Value);
                    return;
                }
            }

            ThrownClientRpc(false, pickedUpObjectId.Value);
            return;
        }

        [Rpc(SendTo.Owner)]
        void ThrownClientRpc(bool success, ulong objectId)
        {
            if (success)
            {
                Debug.Log($"[{nameof(PlayerController)}] {NetworkObjectId} thrown {objectId}");
                pickedUpObjectId.Value = 0;
            }
            
            _interactions &= ~(Interactions.Throw | Interactions.Hold);
        }

        [Rpc(SendTo.Everyone)]
        void RegisterToInGameManagerRpc(ulong objectId)
        {
            InGameManager manager =  GameObject.FindAnyObjectByType<InGameManager>();
            manager.RegisterPlayer(objectId);
        }
    }
}
