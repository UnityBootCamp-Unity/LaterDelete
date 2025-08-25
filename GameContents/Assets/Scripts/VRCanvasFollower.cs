using UnityEngine;

[RequireComponent(typeof(Canvas))]
public class VRCanvasFollower : MonoBehaviour
{
    [Header("Target (Player Head / XR Camera)")]
    public Transform head; // 비워두면 Camera.main 사용

    [Header("Placement")]
    [Tooltip("머리 앞 거리 (m)")]
    public float distance = 1.5f;
    [Tooltip("머리 기준 위(+)/아래(-) 오프셋 (m)")]
    public float heightOffset = 0.0f;
    [Tooltip("머리 기준 오른쪽(+)/왼쪽(-) 오프셋 (m)")]
    public float lateralOffset = 0.0f;

    [Header("Orientation")]
    [Tooltip("Yaw(수평 회전)만 따라가도록 평면화")]
    public bool yawOnly = true;
    [Tooltip("캔버스를 사용자 쪽으로 향하게(=LookAt 유저). 끄면 사용자가 보는 방향을 바라봄(HUD 느낌)")]
    public bool faceUser = false;

    [Header("Smoothing")]
    [Tooltip("이동 스무딩 시간(작을수록 즉각)")]
    public float moveSmoothTime = 0.1f;
    [Tooltip("회전 스무딩 속도(클수록 즉각)")]
    public float rotSmoothSpeed = 12f;

    [Header("Auto Setup")]
    [Tooltip("Canvas.worldCamera를 XR 카메라로 자동 설정")]
    public bool assignWorldCamera = true;

    private Canvas canvas;
    private Camera headCam;
    private Vector3 velocity; // SmoothDamp용

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        if (canvas.renderMode != RenderMode.WorldSpace)
            canvas.renderMode = RenderMode.WorldSpace;

        // head/카메라 자동 할당
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
            // 런타임에 카메라가 늦게 생성되는 경우 대비
            headCam = Camera.main;
            if (headCam != null) head = headCam.transform;
            if (head == null) return;
        }

        // 1) 목표 forward/right 계산
        Vector3 fwd = head.forward;
        if (yawOnly)
        {
            fwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 1e-4f) fwd = head.forward;
        }
        Vector3 right = yawOnly ? Vector3.Cross(Vector3.up, fwd).normalized : head.right;

        // 2) 목표 위치 (머리 앞 distance + 오프셋)
        Vector3 targetPos = head.position + fwd * distance + Vector3.up * heightOffset + right * lateralOffset;

        // 3) 스무딩 이동
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, moveSmoothTime);

        // 4) 스무딩 회전
        Quaternion targetRot;
        if (faceUser)
        {
            // 캔버스 면이 사용자 쪽을 향함
            Vector3 toUser = (head.position - transform.position);
            toUser = yawOnly ? Vector3.ProjectOnPlane(toUser, Vector3.up) : toUser;
            if (toUser.sqrMagnitude < 1e-4f) toUser = fwd; // 안전장치
            targetRot = Quaternion.LookRotation(toUser.normalized, Vector3.up);
        }
        else
        {
            // 사용자가 보는 방향을 함께 바라봄(HUD)
            targetRot = Quaternion.LookRotation(fwd, Vector3.up);
        }

        // 지수 감쇠식 회전 보간 (프레임 독립)
        float t = 1f - Mathf.Exp(-rotSmoothSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    /// <summary>즉시 스냅(초기 배치에 유용)</summary>
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
