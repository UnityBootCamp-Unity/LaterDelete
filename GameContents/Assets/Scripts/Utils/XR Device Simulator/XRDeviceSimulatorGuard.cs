using UnityEngine;
using Utils.Singletons; // �װ� �ø� SingletonMonoBase<T> ���ӽ����̽�

/// <summary>
/// XR Device Simulator�� '�����Ϳ�����' �� �ϳ��� �����ϱ� ���� ����.
/// - SingletonMonoBase<T>�� �״�� Ȱ�� (Awake ������ X)
/// - ���� ���� �ٿ��� ��� (instance ���͸� ��𼭵� ȣ������ �� ��)
/// </summary>
#if UNITY_EDITOR
[DefaultExecutionOrder(-10000)] // ������ �� ���� ����Ǿ� �ߺ�/���� ���� �ּ�ȭ
public class XRDeviceSimulatorGuard : SingletonMonoBase<XRDeviceSimulatorGuard>
{
    private void Start()
    {
        // �����Ϳ����� ����
        if (!Application.isEditor)
        {
            Destroy(gameObject);
            return;
        }

        // ������� ������ Base�� Awake�� ���� ���鼭 _instance�� ���� ����.
        // �� ��ȯ���� �����ǵ��� ����(�� ��ġ ��η� ���� ��� ���)
        /*
         * ���̽��� DontDestroyOnLoad�� �����ͷ� �ڵ� ������ ������ ����˴ϴ�.
         * ���� ��ġ�� ���� �ɸ��� ������ ���忡�� �� �� �� DontDestroyOnLoad(gameObject)�� ȣ���� �ִ� �� �ʼ�����.
         * �̷��� �ϸ� Additive/�� ��ȯ ���������� �׻� 1���� �����ϰ� �����˴ϴ�.
         */
        DontDestroyOnLoad(gameObject);
    }
}
#else
public class XRDeviceSimulatorGuard : MonoBehaviour
{
    private void Awake()
    {
        // ���忡���� �ùķ����� ��� X
        Destroy(gameObject);
    }
}
#endif
