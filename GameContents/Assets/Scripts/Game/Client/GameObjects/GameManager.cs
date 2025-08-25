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
                        // �� �̵� ���� Lobbies UI�� ��ȯ
                        //SwitchToLobbiesUI(State.InWaitingRoom);
                        ChangeState(State.WaitUntilLobbiesSceneLoaded); // �ӽ�
                        StartCoroutine(LoadSceneAsync("Lobbies", State.InLobbies));
                    }
                    break;

                case State.WaitUntilLobbiesSceneLoaded:
                    break;

                case State.InLobbies:
                    break;

                case State.SceneLoadWaitingRoom:
                    {
                        // �ʿ� �� �ٸ� �� �ε��� �״�� ���
                        ChangeState(State.WaitUntilLobbiesSceneLoaded); // �ӽ�
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
        // UI ��ȯ ����: Auth/User(0~4) ��Ȱ��, Lobbies(5~10) Ȱ��
        // ------------------------------------------------------------
        private void SwitchToLobbiesUI(State targetState)
        {
            if (canvases == null || canvases.Length < 11)
            {
                Debug.LogWarning("GameManager: canvases �迭�� �������� �ʾҰų� 11�� �̸��Դϴ�.");
                ChangeState(targetState); // ���� ��ȯ�� �ϴ� ����
                return;
            }

            // ��Ȱ��: 0~4
            for (int i = 0; i <= 4; i++)
                canvases[i].gameObject.SetActive(false);

            // Ȱ��: 5~10
            for (int i = 5; i <= 10; i++)
                canvases[i].gameObject.SetActive(true);

            ChangeState(targetState);
        }

        // ------------------------------------------------------------
        // �� �δ�(���� ���ҷ� ȯ��: UI �ǵ帮�� ����)
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
