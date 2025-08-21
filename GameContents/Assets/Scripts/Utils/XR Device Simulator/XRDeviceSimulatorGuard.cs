using UnityEngine;
using Utils.Singletons; // 네가 올린 SingletonMonoBase<T> 네임스페이스

/// <summary>
/// XR Device Simulator를 '에디터에서만' 단 하나만 유지하기 위한 가드.
/// - SingletonMonoBase<T>를 그대로 활용 (Awake 재정의 X)
/// - 씬에 직접 붙여서 사용 (instance 게터를 어디서도 호출하지 말 것)
/// </summary>
#if UNITY_EDITOR
[DefaultExecutionOrder(-10000)] // 가능한 한 먼저 실행되어 중복/수명 문제 최소화
public class XRDeviceSimulatorGuard : SingletonMonoBase<XRDeviceSimulatorGuard>
{
    private void Start()
    {
        // 에디터에서만 유지
        if (!Application.isEditor)
        {
            Destroy(gameObject);
            return;
        }

        // 여기까지 왔으면 Base의 Awake가 먼저 돌면서 _instance를 잡은 상태.
        // 씬 전환에도 유지되도록 보강(씬 배치 경로로 들어온 경우 대비)
        /*
         * 베이스의 DontDestroyOnLoad는 “게터로 자동 생성될 때만” 실행됩니다.
         * 씬에 배치해 쓰면 걸리지 않으니 가드에서 한 번 더 DontDestroyOnLoad(gameObject)를 호출해 주는 게 필수예요.
         * 이렇게 하면 Additive/씬 전환 구조에서도 항상 1개만 안전하게 유지됩니다.
         */
        DontDestroyOnLoad(gameObject);
    }
}
#else
public class XRDeviceSimulatorGuard : MonoBehaviour
{
    private void Awake()
    {
        // 빌드에서는 시뮬레이터 사용 X
        Destroy(gameObject);
    }
}
#endif
