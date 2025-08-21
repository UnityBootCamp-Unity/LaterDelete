using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace Game.Client.Views
{
    public class LoadingView : MonoBehaviour
    {
        [Header("Canvas - Progress")]
        [SerializeField] Canvas _progress;
        [SerializeField] TMP_Text _progressTitle;
        [SerializeField] TMP_Text _progressPercent;
        [SerializeField] RectTransform _progressFill;


        private void Awake()
        {
            DontDestroyOnLoad(transform.parent.gameObject);
        }

        private void OnEnable()
        {
            SceneTransitionUtility.onLoadBegin += _progress.Show;
            SceneTransitionUtility.onLoadProgress += Refresh;
            SceneTransitionUtility.onLoadEnd += _progress.Hide;
        }

        private void OnDisable()
        {
            SceneTransitionUtility.onLoadBegin -= _progress.Show;
            SceneTransitionUtility.onLoadProgress -= Refresh;
            SceneTransitionUtility.onLoadEnd -= _progress.Hide;
        }

        void Refresh(float progress)
        {
            float fakeProgress = Mathf.Clamp01(progress / 0.9f);
            int fakeProgressPercent = Mathf.RoundToInt(fakeProgress * 100f);
            _progressPercent.text = fakeProgressPercent.ToString();
            _progressFill.anchorMax = new Vector2(fakeProgress, _progressFill.anchorMax.y);
            _progressFill.SetRight(0);
        }
    }
}