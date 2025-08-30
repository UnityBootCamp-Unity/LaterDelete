using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Client;
using UnityEngine;

namespace Assets.Scripts.Game.Client.Views
{
    public class AuthUISwitchLobbyUI : MonoBehaviour
    {
        [Header("Canvas Order (0~10)")]
        [SerializeField] private Canvas[] canvases; // 0~4: Auth/User, 5~10: Lobbies

        private void Start()
        {
            SwitchUI(UserInfoCheck.CheckAuth());
        }


        // ------------------------------------------------------------
        // UI 전환 전용: Auth/User(0~4) 비활성, Lobbies(5~10) 활성
        // ------------------------------------------------------------
        private void SwitchUI(bool checkLogin)
        {
            if (canvases == null || canvases.Length < 11)
            {
                Debug.LogWarning("GameManager: canvases 배열이 설정되지 않았거나 11개 미만입니다.");
                return;
            }

            if (checkLogin)
            {
                // 비활성: 0~4
                for (int i = 0; i <= 4; i++)
                    canvases[i].gameObject.SetActive(false);

                // 활성: 5~10
                for (int i = 5; i <= 10; i++)
                    canvases[i].gameObject.SetActive(true);
                GameManager.instance.ChangeState(State.LoggedIn);
            }
            else
            {
                // 활성: 0~4
                for (int i = 0; i <= 4; i++)
                    canvases[i].gameObject.SetActive(true);
                // 비활성: 5~10
                for (int i = 5; i <= 10; i++)
                    canvases[i].gameObject.SetActive(false);

                GameManager.instance.ChangeState(State.WaitForLogin);

            }
        }
    }
}
