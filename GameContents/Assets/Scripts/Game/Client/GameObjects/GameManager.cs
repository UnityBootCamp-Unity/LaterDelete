using Utils;
using Utils.Singletons;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client
{
    public enum State
    {
        None,
        WaitForLogin,
        LoggedIn,
        WaitUntilLobbiesSceneLoaded,
        InWaitingRoom,
        InLobbies,
        SceneLoadWaitingRoom,
        StartupGamePlay,
        WaitForGamePlay,
        InGamePlay,
    }

    public class GameManager : SingletonMonoBase<GameManager>
    {
        [Header("Canvas Order (0~10)")]
        [SerializeField] private Canvas[] canvases; // 0~4: Auth/User, 5~10: Lobbies

        public State state { get; private set; }
        public event Action<State, State> OnStateChanged;

        private void Update() => Workflow();

        void Workflow()
        {
            switch (state)
            {
                case State.None:
                    break;

                case State.WaitForLogin:
                    break;

                case State.LoggedIn:
                    {
                        // 씬 이동 없이 Lobbies UI로 전환
                        //SwitchToLobbiesUI(State.InWaitingRoom);
                        ChangeState(State.WaitUntilLobbiesSceneLoaded); // 임시
                        StartCoroutine(LoadSceneAsync("Lobbies", State.InLobbies));
                    }
                    break;

                case State.WaitUntilLobbiesSceneLoaded:
                    break;

                case State.InLobbies:
                    break;

                case State.SceneLoadWaitingRoom:
                    {
                        // 필요 시 다른 씬 로딩은 그대로 사용
                        ChangeState(State.WaitUntilLobbiesSceneLoaded); // 임시
                        StartCoroutine(LoadSceneAsync("WaitingRoom", State.InWaitingRoom));
                    }
                    break;

                case State.InWaitingRoom:
                    break;

                case State.StartupGamePlay:
                    {
                        ChangeState(State.WaitForGamePlay);
                        StartCoroutine(SceneTransitionUtility.C_LoadAndSwitchAsync(
                            "InGame", null, null, () => ChangeState(State.InGamePlay)));
                    }
                    break;

                case State.WaitForGamePlay:
                    break;

                case State.InGamePlay:
                    break;

                default:
                    break;
            }
        }

        public void ChangeState(State newState)
        {
            if (state == newState) return;
            var old = state;
            state = newState;
            OnStateChanged?.Invoke(old, newState);
        }

        // ------------------------------------------------------------
        // UI 전환 전용: Auth/User(0~4) 비활성, Lobbies(5~10) 활성
        // ------------------------------------------------------------
        private void SwitchToLobbiesUI(State targetState)
        {
            if (canvases == null || canvases.Length < 11)
            {
                Debug.LogWarning("GameManager: canvases 배열이 설정되지 않았거나 11개 미만입니다.");
                ChangeState(targetState); // 상태 전환은 일단 수행
                return;
            }

            // 비활성: 0~4
            for (int i = 0; i <= 4; i++)
                canvases[i].gameObject.SetActive(false);

            // 활성: 5~10
            for (int i = 5; i <= 10; i++)
                canvases[i].gameObject.SetActive(true);

            ChangeState(targetState);
        }

        // ------------------------------------------------------------
        // 씬 로더(원래 역할로 환원: UI 건드리지 않음)
        // ------------------------------------------------------------
        private IEnumerator LoadSceneAsync(string sceneName, State targetState)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);
            while (!loadOp.isDone)
                yield return null;

            ChangeState(targetState);
        }
    }
}
