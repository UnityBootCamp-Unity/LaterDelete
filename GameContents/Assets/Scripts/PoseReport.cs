// Assets/Scripts/Net/Pickable.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class PoseReport : NetworkBehaviour
{
    // ���� ��� �ִ���: ClientId / Player NetworkObjectId
    public NetworkVariable<ulong> pickerClientId = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> pickerObjectId = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Follow Settings")]
    [Tooltip("Ŭ���̾�Ʈ�� ������ ��� ���� �ֱ�(��)")]
    public float sendInterval = 1f / 60f;
    [Tooltip("ū ���� �̵�(����) ��� �Ÿ�")]
    public float snapDistance = 0.25f;

    Rigidbody rb;

    // ����(���� ���)�� ���: ������ ���� ����Ʈ
    Transform localDriver;     // XR ��/��Ŀ Ʈ������
    float sendTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // NetworkRigidbody + NetworkTransform(���� ����) �� ���� ������Ʈ�� �־�� ��
    }

    public override void OnNetworkDespawn()
    {
        localDriver = null;
    }

    // -------------------- ����(��� Ŭ��)���� ȣ���� API --------------------

    /// <summary>���� �÷��̾ �� ������Ʈ�� ��� ����</summary>
    public void BeginLocalGrab(Transform driverAttach, ulong playerNetworkObjectId)
    {
        localDriver = driverAttach;
        sendTimer = 0f;
        // ������ "���� ���" ��û
        RequestPickServerRpc(NetworkManager.LocalClientId, playerNetworkObjectId);
    }

    /// <summary>���� �÷��̾ �� ������Ʈ�� ����</summary>
    public void EndLocalGrab()
    {
        localDriver = null;
        // ������ "������" ��û
        RequestDropServerRpc(NetworkManager.LocalClientId);
    }

    void Update()
    {
        // ���� ��� ���� ���� ���� ����(���� �����̹Ƿ� ServerRpc)
        if (localDriver != null)
        {
            sendTimer += Time.deltaTime;
            if (sendTimer >= sendInterval)
            {
                sendTimer = 0f;
                var p = localDriver.position;
                var r = localDriver.rotation;
                ReportPoseServerRpc(p, r);
            }
        }
    }

    // -------------------- ���� ���� --------------------

    [ServerRpc(RequireOwnership = false)]
    void RequestPickServerRpc(ulong requesterClientId, ulong playerObjectId)
    {
        // �̹� �ٸ� Ŭ���̾�Ʈ�� ��� ������ ����(���� ����� ���� ���)
        if (pickerClientId.Value != 0 && pickerClientId.Value != requesterClientId)
            return;

        pickerClientId.Value = requesterClientId;
        pickerObjectId.Value = playerObjectId;

        // ��� �ִ� ���� ���� ����ȭ�� ���� kinematic ����
        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    [ServerRpc(RequireOwnership = false)]
    void RequestDropServerRpc(ulong requesterClientId)
    {
        if (pickerClientId.Value != requesterClientId) return;

        pickerClientId.Value = 0;
        pickerObjectId.Value = 0;

        // ���� ����
        rb.isKinematic = false;
    }

    // Ŭ���̾�Ʈ �� ����: �� ���� ����Ʈ (Unreliable�� ��� ����)
    [ServerRpc(RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
    void ReportPoseServerRpc(Vector3 targetPos, Quaternion targetRot, ServerRpcParams rpcParams = default)
    {
        // ���� ����� ���� Ȧ������ Ȯ��
        if (rpcParams.Receive.SenderClientId != pickerClientId.Value) return;

        // ���� ������ Rigidbody �̵�
        if (rb.isKinematic)
        {
            // �̵����� �ʹ� ũ�� ����
            if ((rb.position - targetPos).sqrMagnitude > snapDistance * snapDistance)
            {
                rb.position = targetPos;
                rb.rotation = targetRot;
            }
            else
            {
                rb.MovePosition(targetPos);
                rb.MoveRotation(targetRot);
            }
        }
        else
        {
            // ��-Ű�׸�ƽ�� ��쿡�� ���� ����(���� ��Ȳ ���)
            rb.position = targetPos;
            rb.rotation = targetRot;
        }
        // NetworkTransform(���� ����)�� ������ Ŭ���̾�Ʈ�� ����
    }
}
