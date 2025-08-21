using Game.Lobbies;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Views
{
    public class LobbyInfoSlot : MonoBehaviour
    {
        public int Id { get; private set; }


        [SerializeField] TMP_Text _id;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _numClient;
        [SerializeField] TMP_Text _maxClient;
        [SerializeField] Button _select;
        [SerializeField] Image _selectedOutline;

        public event Action<int> onSelectButtonClicked;


        private void Start()
        {
            _select.onClick.AddListener(() =>
            {
                onSelectButtonClicked?.Invoke(Id);
            });
        }

        public void Refresh(LobbyInfo info)
        {
            Id = info.LobbyId;
            _id.text = info.LobbyId.ToString();
            _title.text = info.CustomProperties[LOBBY_NAME];
            _numClient.text = info.NumClient.ToString();
            _maxClient.text = info.MaxClient.ToString();
        }

        public void Select() => _selectedOutline.enabled = true;
        public void Deselect() => _selectedOutline.enabled = false;
    }
}
