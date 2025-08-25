using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class VRCanvasFollower : MonoBehaviour
{
    [Header("Target (Player Head / XR Camera)")]
    public Transform head; // ����θ� Camera.main ���

    [Header("Placement")]
    [Tooltip("�Ӹ� �� �Ÿ� (m)")]
    public float distance = 1.5f;
    [Tooltip("�Ӹ� ���� ��(+)/�Ʒ�(-) ������ (m)")]
    public float heightOffset = 0.0f;
    [Tooltip("�Ӹ� ���� ������(+)/����(-) ������ (m)")]
    public float lateralOffset = 0.0f;

    [Header("Orientation")]
    [Tooltip("Yaw(���� ȸ��)�� ���󰡵��� ���ȭ")]
    public bool yawOnly = true;
    [Tooltip("ĵ������ ����� ������ ���ϰ�(=LookAt ����). ���� ����ڰ� ���� ������ �ٶ�(HUD ����)")]
    public bool faceUser = false;

    [Header("Smoothing")]
    [Tooltip("�̵� ������ �ð�(�������� �ﰢ)")]
    public float moveSmoothTime = 0.1f;
    [Tooltip("ȸ�� ������ �ӵ�(Ŭ���� �ﰢ)")]
    public float rotSmoothSpeed = 12f;

    [Header("Auto Setup")]
    [Tooltip("Canvas.worldCamera�� XR ī�޶�� �ڵ� ����")]
    public bool assignWorldCamera = true;

    private Canvas canvas;
    private Camera headCam;
    private Vector3 velocity; // SmoothDamp��

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas.renderMode != RenderMode.WorldSpace)
            canvas.renderMode = RenderMode.WorldSpace;

        // head/ī�޶� �ڵ� �Ҵ�
        headCam = Camera.main;
        if (head == null && headCam != null)
            head = headCam.transform;

        if (assignWorldCamera && headCam != null)
            canvas.worldCamera = headCam;
    }

    void LateUpdate()
    {
        if (head == null)
        {
            // ��Ÿ�ӿ� ī�޶� �ʰ� �����Ǵ� ��� ���
            headCam = Camera.main;
            if (headCam != null) head = headCam.transform;
            if (head == null) return;
        }

        // 1) ��ǥ forward/right ���
        Vector3 fwd = head.forward;
        if (yawOnly)
        {
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = head.forward;
        }
        Vector3 right = yawOnly ? Vector3.Cross(Vector3.up, fwd).normalized : head.right;

        // 2) ��ǥ ��ġ (�Ӹ� �� distance + ������)
        Vector3 targetPos = head.position + fwd * distance + Vector3.up * heightOffset + right * lateralOffset;

        // 3) ������ �̵�
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, moveSmoothTime);

        // 4) ������ ȸ��
        Quaternion targetRot;
        if (faceUser)
        {
            // ĵ���� ���� ����� ���� ����
            Vector3 toUser = (head.position - transform.position);
            toUser = yawOnly ? Vector3.ProjectOnPlane(toUser, Vector3.up) : toUser;
            if (toUser.sqrMagnitude < 1e-4f) toUser = fwd; // ������ġ
            targetRot = Quaternion.LookRotation(toUser.normalized, Vector3.up);
        }
        else
        {
            // ����ڰ� ���� ������ �Բ� �ٶ�(HUD)
            targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // ���� ����� ȸ�� ���� (������ ����)
        float t = 1f - Mathf.Exp(-rotSmoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    /// <summary>��� ����(�ʱ� ��ġ�� ����)</summary>
    [ContextMenu("Snap Now")]
    public void SnapNow()
    {
        if (head == null) return;

        Vector3 fwd = head.forward;
        if (yawOnly)
        {
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = head.forward;
        }
        Vector3 right = yawOnly ? Vector3.Cross(Vector3.up, fwd).normalized : head.right;
        Vector3 pos = head.position + fwd * distance + Vector3.up * heightOffset + right * lateralOffset;

        transform.position = pos;
        transform.rotation = faceUser
            ? Quaternion.LookRotation((head.position - pos).normalized, Vector3.up)
            : Quaternion.LookRotation(fwd, Vector3.up);
    }
}
