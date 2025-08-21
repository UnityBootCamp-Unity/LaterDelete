using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utils
{
    public static class SceneTransitionUtility
    {
        const string LOADING_SCENE_NAME = "Loading";
        public static event Action onLoadBegin;
        public static event Action<float> onLoadProgress;
        public static event Action onLoadEnd;

        [RuntimeInitializeOnLoadMethod]
        static void PrepareLoadingScene()
        {
            SceneManager.LoadScene(LOADING_SCENE_NAME, LoadSceneMode.Additive);
        }

        public static IEnumerator C_LoadAndSwitchAsync(string nextSceneName,
                                                       Action onBegin = null,
                                                       Action<float> onProgress = null,
                                                       Action onEnd = null,
                                                       bool waitForActivation = true)
        {
            onLoadBegin?.Invoke();
            onBegin?.Invoke();

            List<Scene> oldScenes = new List<Scene>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (scene.name == LOADING_SCENE_NAME)
                    continue;

                oldScenes.Add(scene);
            }

            AsyncOperation loadOp = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);

            if (loadOp == null)
            {
                Debug.LogError($"[{nameof(SceneTransitionUtility)}] Failed to start load scene {nextSceneName}");
                yield break;
            }

            loadOp.allowSceneActivation = !waitForActivation; // loadOp 가 끝날때까지 로딩중인 씬을 활성화하지않음 (로딩창띄워놓고 기다릴때 씀)

            while (loadOp.progress < 0.9f)
            {
                onLoadProgress?.Invoke(loadOp.progress);
                onProgress?.Invoke(loadOp.progress);

                yield return null;
            }

            if (waitForActivation)
            {
                loadOp.allowSceneActivation = true;
            }

            while (loadOp.isDone == false)
            {
                yield return null;
            }
                
            Scene loaded = SceneManager.GetSceneByName(nextSceneName);

            if (loaded.IsValid())
            {
                SceneManager.SetActiveScene(loaded);
            }

            // 이전 씬 언로드
            foreach (var scene in oldScenes)
            {
                if (scene.IsValid())
                {
                    var unloadOp = SceneManager.UnloadSceneAsync(scene);

                    if (unloadOp != null)
                        while (unloadOp.isDone == false)
                            yield return null;
                }
            }

            onLoadEnd?.Invoke(); // 로딩창 닫기 등..
            onEnd?.Invoke();
        }
    }
}
