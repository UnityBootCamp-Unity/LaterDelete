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
            _nickname.text = "User" + clientId.ToString(); // TODO : User 서비스로부터 닉네임 가져오기

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
