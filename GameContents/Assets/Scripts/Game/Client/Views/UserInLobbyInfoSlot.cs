using System.Collections.Generic;
using Game.Client.Controllers;
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
        [SerializeField] Button _readyButton;

        LobbiesController _lobbiesController;     // 컨트롤러 참조
        bool _isToggling;                // 중복 클릭 가드
        bool _isMe;                // 내 슬롯인지 캐싱

        // 슬롯 생성 직후 뷰에서 한 번만 호출
        public void Init(LobbiesController lc)
        {
            _lobbiesController = lc;
            if (_readyButton == null) _readyButton = GetComponentInChildren<Button>(true);
            _readyButton.onClick.RemoveAllListeners();
            _readyButton.onClick.AddListener(OnClickReady);
        }

        void OnDestroy()
        {
            if (_readyButton != null)
                _readyButton.onClick.RemoveListener(OnClickReady);
        }

        public void Refresh(UserInLobbyInfo info)
        {
            clientId = info.ClientId;
            _isMe = (clientId == GrpcConnection.clientInfo.ClientId);

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
                if (bool.Parse(isReadyString))
                {
                    Debug.Log($"ClientId: {clientId} is Ready.");
                    _readyButton.GetComponentInChildren<TMP_Text>().text = "Ready";
                    _readyButton.targetGraphic.color = Color.green;
                }
                else
                {
                    Debug.Log($"ClientId: {clientId} is Not Ready.");
                    _readyButton.GetComponentInChildren<TMP_Text>().text = "NotReady";
                    _readyButton.targetGraphic.color = Color.red;
                }

            }
            else
            {
                _isReady.enabled = false;
            }

            // Is master ?
            if (info.CustomProperties.TryGetValue(IS_MASTER, out string isMasterString))
            {
                _isMaster.enabled = bool.Parse(isMasterString);
                if (bool.Parse(isMasterString))
                {
                    Debug.Log($"ClientId: {clientId} is Master.");
                    _readyButton.interactable = false;
                }
                else
                {
                    Debug.Log($"ClientId: {clientId} is Not Master.");
                    _readyButton.interactable = true;
                }
            }
            else
            {
                _isMaster.enabled = false;
                _readyButton.interactable = true;
            }
        }

        async void OnClickReady()
        {
            if (_lobbiesController == null || !_isMe || _isToggling) return;
            if (_lobbiesController.isMaster) return; // 방장은 토글 금지(혹시 모를 이중 보호)

            _isToggling = true;
            _readyButton.interactable = false;

            try
            {
                // 기존 프로퍼티 복사 후 IS_READY만 토글
                var props = new Dictionary<string, string>();
                if (_lobbiesController.userCustomProperties.TryGetValue(clientId, out var cur))
                    foreach (var kv in cur) props[kv.Key] = kv.Value;

                bool nowReady = props.TryGetValue(IS_READY, out var s) && bool.TryParse(s, out var b) && b;
                props[IS_READY] = (!nowReady).ToString();

                var (ok, msg) = await _lobbiesController.SetUserCustomPropertiesAsync(clientId, props);
                if (!ok) Debug.LogWarning($"Toggle ready failed: {msg}");
                // 실제 UI 갱신은 onUserPropsChanged → Refresh() 경로에서 다시 반영
            }
            finally
            {
                _isToggling = false;
                // 내 슬롯이면 다시 인터랙션 열기 (마스터가 승격됐으면 Refresh에서 막힘)
                _readyButton.interactable = _isMe && !_lobbiesController.isMaster;
            }
        }

        bool TryGetBool(IDictionary<string, string> dict, string key, out bool value)
        {
            value = false;
            return dict != null && dict.TryGetValue(key, out var s) && bool.TryParse(s, out value);
        }
    }
}
