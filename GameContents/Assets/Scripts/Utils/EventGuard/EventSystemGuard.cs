using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.Utils.EventGuard
{
    public class EventSystemGuard : MonoBehaviour
    {
#if UNITY_CLIENT
        private static EventSystemGuard instance;

        void Awake()
        {
            // 이미 인스턴스가 있으면 현재 오브젝트를 삭제
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            // 인스턴스 설정 및 DontDestroyOnLoad 적용
            instance = this;
            DontDestroyOnLoad(gameObject);

            // EventSystem이 없으면 추가
            if (GetComponent<EventSystem>() == null)
            {
                gameObject.AddComponent<EventSystem>();
            }

            // StandaloneInputModule이 없으면 추가
            if (GetComponent<StandaloneInputModule>() == null)
            {
                gameObject.AddComponent<StandaloneInputModule>();
            }

            Debug.Log("EventSystem이 DontDestroyOnLoad로 설정되었습니다.");
        }

        // 다른 씬에서 EventSystem 중복 체크 및 정리
        void Start()
        {
            CleanupDuplicateEventSystems();
        }
#endif

        void CleanupDuplicateEventSystems()
        {
            EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();

            if (eventSystems.Length > 1)
            {
                Debug.Log($"중복된 EventSystem {eventSystems.Length}개 발견. 정리 중...");

                foreach (EventSystem es in eventSystems)
                {
                    // 현재 인스턴스가 아닌 EventSystem들을 삭제
                    if (es.gameObject != this.gameObject)
                    {
                        Debug.Log($"중복된 EventSystem 삭제: {es.gameObject.name}");
                        Destroy(es.gameObject);
                    }
                }
            }
        }

        // 씬이 로드될 때마다 호출되는 이벤트 (Unity 2018.1 이상)
        void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // 새 씬이 로드될 때마다 중복 EventSystem 정리
            Invoke(nameof(CleanupDuplicateEventSystems), 0.1f);
        }
    }
}
