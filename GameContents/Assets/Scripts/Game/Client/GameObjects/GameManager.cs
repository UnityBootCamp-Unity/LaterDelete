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
        InLobbies,
        InWaitingRoom,
        StartupGamePlay,
        WaitForGamePlay,
        InGamePlay,
    }



    public class GameManager : SingletonMonoBase<GameManager>
    {
        public State state { get; private set; }

        public event Action<State, State> OnStateChanged;

        private void Update()
        {
            Workflow();
        }

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
                        ChangeState(State.WaitUntilLobbiesSceneLoaded);
                        StartCoroutine(LoadSceneAsync("Lobbies", State.InLobbies));
                    }
                    break;
                case State.WaitUntilLobbiesSceneLoaded:
                    break;
                case State.InLobbies:
                    break;
                case State.InWaitingRoom:
                    {
                        StartCoroutine(SceneTransitionUtility.C_LoadAndSwitchAsync("WaitingRoom"));
                    }
                    break;
                case State.StartupGamePlay:
                    {
                        ChangeState(State.WaitForGamePlay);
                        StartCoroutine(SceneTransitionUtility.C_LoadAndSwitchAsync("InGame", null, null, () => ChangeState(State.InGamePlay)));
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
            if (state == newState)
                return;

            State old = state;
            state = newState;
            OnStateChanged?.Invoke(old, newState);
        }


        /// <summary>
        /// 씬 전환 후 상태 변경
        /// </summary>
        private IEnumerator LoadSceneAsync(string sceneName, State targetState)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName);

            while (!loadOp.isDone)
            {
                // loadOp.progress; // ui 에 진행상태 띄워야되면 이거 쓰삼
                yield return null;
            }

            ChangeState(targetState);
        }
    }
}