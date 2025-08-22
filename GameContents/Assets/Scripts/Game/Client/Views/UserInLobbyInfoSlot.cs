using Game.Client.Network;
using Game.Lobbies;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Views
{
    public class UserInLobbyInfoSlot : MonoBehaviour
    {
        public int clientId { get; set; }

        [SerializeField] TMP_Text _nickname;
        [SerializeField] TMP_Text _isReady;
        [SerializeField] Image _isMaster;


        public void Refresh(UserInLobbyInfo info)
        {
            clientId = info.ClientId;
            //_nickname.text = "User" + clientId.ToString(); // TODO : User 서비스로부터 닉네임 가져오기
            // UserId 표시 (모든 사용자)
            if (info.CustomProperties.TryGetValue(USER_ID, out string userId))
            {
                _nickname.text = userId; // 실제 UserId 표시
                // 정상적으로 UserId가 설정되었을 때의 로그
                Debug.Log($"UserInLobbyInfoSlot - ClientId: {clientId}, UserId: {userId}");
            }
            else
            {
                _nickname.text = "Loading..."; // 아직 UserId가 동기화되지 않음
                // USER_ID가 없어서 "Loading..."이 표시되는 경우의 경고 로그
                Debug.LogWarning($"USER_ID missing for ClientId: {clientId}");
            }

            // Is ready ?
            if (info.CustomProperties.TryGetValue(IS_READY, out string isReadyString))
            {
                _isReady.enabled = bool.Parse(isReadyString);
            }
            else
            {
                _isReady.enabled = false;
            }

            // Is master ?
            if (info.CustomProperties.TryGetValue(IS_MASTER, out string isMasterString))
            {
                _isMaster.enabled = bool.Parse(isMasterString);
            }
            else
            {
                _isMaster.enabled = false;
            }
        }
    }
}
