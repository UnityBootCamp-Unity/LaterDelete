using Game.Client.Controllers;
using Game.Client.Network;
using Game.Lobbies;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utils;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Views
{
    public class LobbiesView : MonoBehaviour
    {
        [Header("Controller")]
        [SerializeField] LobbiesController _controller;

        [Header("Canvas - LobbyList")]
        [SerializeField] Canvas _lobbyList;
        [SerializeField] Transform _lobbyListContent;
        [SerializeField] Button _lobbyListRefreshList;
        [SerializeField] Button _lobbyListCreateLobby;
        [SerializeField] Button _lobbyListJoinLobby;
        LobbyInfoSlot _lobbyListSlot;
        Dictionary<int, LobbyInfoSlot> _lobbyListSlots;
        int _selectedLobbyId = -1;

        [Header("Canvas - CreateLobby")]
        [SerializeField] Canvas _createLobby;
        [SerializeField] TMP_InputField _createLobbyName;
        [SerializeField] TMP_InputField _createLobbyMaxClient;
        [SerializeField] Button _createLobbyConfirm;
        [SerializeField] Button _createLobbyCancel;

        [Header("Canvas - InLobby")]
        [SerializeField] Canvas _inLobby;
        [SerializeField] Transform _inLobbyContent;
        [SerializeField] Button _inLobbyReady;
        [SerializeField] Button _inLobbyReadyCancel;
        [SerializeField] Button _inLobbyPlay;
        [SerializeField] Button _inLobbyLeave;
        UserInLobbyInfoSlot _userInLobbySlot;
        List<UserInLobbyInfoSlot> _userInLobbySlots;
        string _lobbyState;

        [Header("Canvas - GameStarting")]
        [SerializeField] Canvas _gameStarting;

        [Header("Canvas - Loading")]
        [SerializeField] Canvas _loading;

        [Header("UI (Canvas - Alert)")]
        [SerializeField] Canvas _alert;
        [SerializeField] TMP_Text _alertMessage;


        private void Start()
        {
            _lobbyListSlot = _lobbyListContent.GetChild(0).GetComponent<LobbyInfoSlot>();
            _lobbyListSlot.gameObject.SetActive(false);
            _lobbyListSlots = new Dictionary<int, LobbyInfoSlot>(); // 로비목록은 수십개이상이 될수있음. random 한 lobbyId 로 인덱스 검색 용이하려면 Dictionary 이 나음

            _userInLobbySlot = _inLobbyContent.GetChild(0).GetComponent<UserInLobbyInfoSlot>();
            _userInLobbySlot.gameObject.SetActive(false);
            _userInLobbySlots = new List<UserInLobbyInfoSlot>(8); // 이 게임은 8명을 초과하는 인게임 컨텐츠가없다. 이정도로 적은 데이터는 선형 O(N)탐색이 Hash O(1) 탐색보다 저렴하다.
        }

        private void OnEnable()
        {
            _lobbyListRefreshList.onClick.AddListener(RefreshLobbyList);
            _lobbyListJoinLobby.onClick.AddListener(OnLobbyListJoinLobbyButtonClicked);
            _lobbyListCreateLobby.onClick.AddListener(_createLobby.Show);
            _createLobbyCancel.onClick.AddListener(_createLobby.Hide);
            _createLobbyConfirm.onClick.AddListener(OnCreateLobbyConfirmButtonClicked);
            _inLobbyReady.onClick.AddListener(OnInLobbyReadyButtonClicked);
            _inLobbyReadyCancel.onClick.AddListener(OnInLobbyReadyCancelButtonClicked);
            _inLobbyLeave.onClick.AddListener(OnInLobbyLeaveButtonClicked);
            _inLobbyPlay.onClick.AddListener(OnInLobbyPlayButtonClicked);
            _controller.onMemberJoin += OnMemberJoin;
            _controller.onMemberLeft += OnMemberLeft;
            _controller.onLobbyPropsChanged += OnLobbyPropsChanged;
            _controller.onUserPropsChanged += OnUserPropsChanged;

        }

        private void OnDisable()
        {
            _lobbyListRefreshList.onClick.RemoveListener(RefreshLobbyList);
            _lobbyListJoinLobby.onClick.RemoveListener(OnLobbyListJoinLobbyButtonClicked);
            _lobbyListCreateLobby.onClick.RemoveListener(_createLobby.Show);
            _createLobbyCancel.onClick.RemoveListener(_createLobby.Hide);
            _createLobbyConfirm.onClick.RemoveListener(OnCreateLobbyConfirmButtonClicked);
            _inLobbyReady.onClick.RemoveListener(OnInLobbyReadyButtonClicked);
            _inLobbyReadyCancel.onClick.RemoveListener(OnInLobbyReadyCancelButtonClicked);
            _inLobbyLeave.onClick.RemoveListener(OnInLobbyLeaveButtonClicked);
            _inLobbyPlay.onClick.RemoveListener(OnInLobbyPlayButtonClicked);
            _controller.onMemberJoin -= OnMemberJoin;
            _controller.onMemberLeft -= OnMemberLeft;
            _controller.onLobbyPropsChanged -= OnLobbyPropsChanged;
            _controller.onUserPropsChanged -= OnUserPropsChanged;
        }

        #region Canvas - LobbyList
        private async void RefreshLobbyList()
        {
            _loading.Show();

            // 남아있던 슬롯 다 파괴 
            // TODO : 풀링 시스템
            foreach (var id in _lobbyListSlots.Keys.ToList())
            {
                Destroy(_lobbyListSlots[id].gameObject);
                _lobbyListSlots.Remove(id);
            }

            var (success, lobbylist) = await _controller.GetLobbyListAsync();

            if (success)
            {
                foreach (var lobbyInfo in lobbylist)
                {
                    if (lobbyInfo.CustomProperties.TryGetValue(LOBBY_NAME, out string lobbyName) == false)
                        continue;

                    LobbyInfoSlot slot = Instantiate(_lobbyListSlot, _lobbyListContent);
                    slot.Refresh(lobbyInfo);
                    slot.onSelectButtonClicked += OnSelectLobbyInfoSlot;
                    slot.gameObject.SetActive(true);
                    _lobbyListSlots[lobbyInfo.LobbyId] = slot;
                }
            }

            _loading.Hide();
            _selectedLobbyId = -1;
            _lobbyListJoinLobby.interactable = false; // 새로 리스트 갱신됐으면 들어가려는 로비는 다시 선택해야함.
        }

        private void OnSelectLobbyInfoSlot(int lobbyId)
        {
            // 전에 선택한 로비 선택해제
            if (_selectedLobbyId >= 0)
            {
                if (_lobbyListSlots.TryGetValue(_selectedLobbyId, out LobbyInfoSlot prevSlot))
                {
                    prevSlot.Deselect();
                }
            }

            _lobbyListSlots[lobbyId].Select();
            _selectedLobbyId = lobbyId;
            _lobbyListJoinLobby.interactable = true; // 선택된 로비가 있어야 Join 클릭가능
        }

        /// <summary>
        /// Join 버튼 눌렀을때
        /// 1. Lobby join 요청
        /// 2. join 성공시 lobbylist canvas 끄고 inlobby canvas 키고
        /// 3. inlobby canvas 컨텐츠 업데이트
        /// </summary>
        private async void OnLobbyListJoinLobbyButtonClicked()
        {
            if (_selectedLobbyId < 0)
            {
                RefreshLobbyList();
                // TODO : 선택된 로비 없음 알림창 띄우기
                return;
            }

            _loading.Show();

            var (success, message, userInfos) = await _controller.JoinLobbyAsync(_selectedLobbyId);

            if (success)
            {
                RefreshUserInLobbyContent(userInfos);

                await _controller.SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
                {
                    { IS_MASTER, bool.FalseString }, // 난 방장 아님
                    { IS_READY, bool.FalseString }, // 일단 준비 안함
                    { USER_ID, GrpcConnection.clientInfo.UserId } // UserId 저장
                });

                _lobbyList.Hide(); // TODO : Canvas 만 숨길게 아니라, View 컴포넌트 비활성화도 해야함. (View 추상화 필요)
            }
            else
            {
                _alertMessage.text = $"Failed to join lobby.\n{message}";
                _alert.Show();
                RefreshLobbyList();
                await Task.Delay(2000);
                _alert.Hide();
            }

            _loading.Hide();
        }
        #endregion

        #region Canvas - CreateLobby
        /// <summary>
        /// CreateLobby canvas의 Confirm 버튼 눌렀을때
        /// 1. Lobby Create 요청
        /// 2. create 성공시 createlobby canvas 끄고 lobbylist canvas 끄고 inlobby canvas 키고
        /// 3. inlobby canvas 컨텐츠 업데이트
        /// </summary>
        private async void OnCreateLobbyConfirmButtonClicked()
        {
            _loading.Show();

            int maxClient = int.Parse(_createLobbyMaxClient.text);
            var (success, message, userInfos) = await _controller.CreateLobbyAsync(maxClient);

            if (success)
            {
                RefreshUserInLobbyContent(userInfos);

                await _controller.SetLobbyCustomPropertiesAsync(new Dictionary<string, string>
                {
                    { LOBBY_NAME, _createLobbyName.text },
                    { LOBBY_STATE, WAITING_FOR_ALL_READY }
                });

                await _controller.SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
                {
                    { IS_MASTER, bool.TrueString }, // 내가 방장
                    { IS_READY, bool.TrueString }, // 방장은 항상 준비완료
                    { USER_ID, GrpcConnection.clientInfo.UserId } // UserId 저장
                });

                _createLobby.Hide();
                _lobbyList.Hide();
            }

            _loading.Hide();

            _alertMessage.text = message;
            _alert.Show();
            await Task.Delay(2000);
            _alert.Hide();
        }
        #endregion

        #region Canvas - InLobby
        private void RefreshUserInLobbyContent(IList<UserInLobbyInfo> infos)
        {
            // 기존슬롯 다 파괴 
            // TODO : 풀링 시스템
            foreach (var slot in _userInLobbySlots.ToList())
            {
                Destroy(slot.gameObject);
                _userInLobbySlots.Remove(slot);
            }

            foreach (var info in infos)
            {
                var slot = Instantiate(_userInLobbySlot, _inLobbyContent);
                slot.Refresh(info);
                slot.gameObject.SetActive(true);
                _userInLobbySlots.Add(slot);
            }
        }

        /// <summary>
        /// 로비내에서 Ready 버튼 누름
        /// 1. Ready버튼 못누르게 막음
        /// 2. Ready 요청
        /// 3. Ready 버튼을 비활성화, ReadyCancel 버튼 활성화
        /// </summary>
        /// <exception cref="System.Exception"></exception>
        private async void OnInLobbyReadyButtonClicked()
        {
            if (_controller.isMaster)
                throw new System.Exception("Master cannot set ready state.");

            _inLobbyReady.interactable = false; // Ready 진행중에는 버튼 또 누를수 없게 

            // 서버에 Ready 요청
            var response = await _controller.SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
            {
                { IS_READY, bool.TrueString}
            });

            // Ready 성공하면
            if (response.success)
            {
                _inLobbyReady.gameObject.SetActive(false);
                _inLobbyReadyCancel.gameObject.SetActive(true);
                _inLobbyReadyCancel.interactable = true;
            }

            _inLobbyReady.interactable = true;
        }

        /// <summary>
        /// 로비내에서 ReadyCancel 버튼 누름
        /// 1. ReadyCancel 버튼 못누르게 막음
        /// 2. ReadyCancel 요청
        /// 3. ReadyCancel 버튼을 비활성화, Ready 버튼 활성화
        /// </summary>
        /// <exception cref="System.Exception"></exception>
        private async void OnInLobbyReadyCancelButtonClicked()
        {
            if (_controller.isMaster)
                throw new System.Exception("Master cannot set ready state.");

            _inLobbyReadyCancel.interactable = false; // ReadyCancel 진행중에는 버튼 또 누를수 없게 

            // 서버에 Ready cancel 요청
            var response = await _controller.SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, new Dictionary<string, string>
            {
                { IS_READY, bool.FalseString }
            });

            // Ready cancel 성공하면
            if (response.success)
            {
                _inLobbyReadyCancel.gameObject.SetActive(false);
                _inLobbyReady.gameObject.SetActive(true);
                _inLobbyReady.interactable = true;
            }
            else
            {
                _inLobbyReadyCancel.interactable = true; // Ready cancel 실패하면 다시 Ready cancel 요청할수있게
            }
        }

        /// <summary>
        /// 로비에서 나갔다는 알림창 잠깐 띄웠다가 
        /// InLobby canvas 비활성화, LobbyList canvas 활성화
        /// 로비에서 나갔다는 알림창 끔
        /// </summary>
        private async void OnInLobbyLeaveButtonClicked()
        {
            _alertMessage.text = "Leave lobby...";
            _alert.Show();
            var (success, message) = await _controller.LeaveLobbyAsync();
            
            if (success)
            {
                _inLobby.Hide();
                _lobbyList.Show();
                _alert.Hide();

                RefreshLobbyList();
            }
            else
            {
                _alertMessage.text = $"Failed to Leave lobby...\n{message}";
                await Task.Delay(2000);
                _alert.Hide();
            }
        }

        private async void OnInLobbyPlayButtonClicked()
        {
            _inLobbyPlay.interactable = false;

            var (success, message) = await _controller.SetLobbyCustomPropertiesAsync(new Dictionary<string, string>
            {
                { LOBBY_STATE, FINISHED_ALL_READY_TO_PLAY_GAME }
            });

            if (success)
            {
                MultiplayMatchBlackboard.isMaster = true;

                MultiplayMatchBlackboard.lobbyUsers = new Dictionary<int, string>(_controller.CurrentLobbyUsers);

                // Nothing to do... 서버 할당되어서 이벤트 처리될때까지 그냥 기다림.
            }
            else
            {
                _alertMessage.text = "Failed to play game...";
                _alert.Show();
                await Task.Delay(2000);
                _alert.Hide();
                _inLobbyPlay.interactable = true;
            }
        }

        /// <summary>
        /// 다른클라이언트 진입시 해당 유저에 대한 슬롯은 생성하지만 아직 해당 유저 데이터로 갱신되지 않았으므로 슬롯을 활성화하지않음.
        /// </summary>
        /// <param name="clientId"> 새로 로비에 들어온 멤버 </param>
        private async void OnMemberJoin(int clientId)
        {
            // Unity 의 Awaitable 은
            // C# 의 SynchronizationContext 를 기반으로한 MainThread Send/Post 등의 동기화를 할수있는 함수들을 제공하는 클래스.
            // SynchronizationContext 에서 Send 랑 Post 가 뭐가다름 ? 
            // Send (Awaitable.MainThreadAsync) 는 : 동기화해야하는 쓰레드컨텍스트랑 현재실행 쓰레드가 같으면 동기실행, 다르면 동기화해야하는 실행 Queue 등록
            // Post (Awaitable.NextFrameAsync) 는 : 그냥 동기화해야하는 쓰레드컨텍스트 실행 Queue 등록
            await Awaitable.MainThreadAsync(); // 이거 이해 잘 안되면 AI 한테, 이 함수 Awaitable 쓰지말고 SynchronizationContext 기반으로 다시 짜달라고 하삼.

            Debug.Log("OnMemberJoin");

            var slot = Instantiate(_userInLobbySlot, _inLobbyContent);
            slot.clientId = clientId;
            slot.gameObject.SetActive(false); // 아직 유저 데이터가 없으므로 비활성화(UserId 받을 때까지 비활성화)
            _userInLobbySlots.Add(slot);
        }

        /// <summary>
        /// 나간 유저의 슬롯 제거
        /// </summary>
        /// <param name="clientId"> 로비에서 나간 멤버 </param>
        private async void OnMemberLeft(int clientId)
        {
            await Awaitable.MainThreadAsync();

            Debug.Log("OnMemberLeft");

            int slotIndex = _userInLobbySlots.FindIndex(slot => slot.clientId == clientId);
            Destroy(_userInLobbySlots[slotIndex].gameObject);
            _userInLobbySlots.RemoveAt(slotIndex);
        }

        private async void OnUserPropsChanged(int clientId, IDictionary<string, string> props)
        {
            await Awaitable.MainThreadAsync();

            Debug.Log("OnUserPropsChanged");
            int slotIndex = _userInLobbySlots.FindIndex(slot => slot.clientId == clientId);

            if (slotIndex < 0)
                return;

            _userInLobbySlots[slotIndex].Refresh(new UserInLobbyInfo
            {
                ClientId = clientId,
                CustomProperties = { props }
            });

            if (props.ContainsKey(USER_ID))
            {
                // UserId 가 동기화되었으면 이제 슬롯 활성화
                _userInLobbySlots[slotIndex].gameObject.SetActive(true);
            }

            if (clientId == GrpcConnection.clientInfo.ClientId)
            {
                RefreshInLobbyUIs();
            }

            if (_controller.isMaster)
                CheckAllReady();
        }

        private async void OnLobbyPropsChanged(IDictionary<string, string> props)
        {
            await Awaitable.MainThreadAsync();

            Debug.Log("OnLobbyPropsChanged");
            // TODO : 로비 제목 변경, 최대인원수 변경 같은거 처리

            if (props.TryGetValue(LOBBY_STATE, out string newLobbyState))
                OnLobbyStateChanged(newLobbyState);
        }

        private void RefreshInLobbyUIs()
        {
            bool isMaster = _controller.isMaster;

            if (isMaster)
            {
                _inLobbyPlay.gameObject.SetActive(true);
                _inLobbyReady.gameObject.SetActive(false);
                _inLobbyReadyCancel.gameObject.SetActive(false);
            }
            else
            {
                _inLobbyPlay.gameObject.SetActive(false);
                bool isReady = false;

                if (_controller.myUsercustomProperties.TryGetValue(IS_READY, out string isReadyString))
                    isReady = bool.Parse(isReadyString);

                _inLobbyReady.gameObject.SetActive(!isReady);
                _inLobbyReadyCancel.gameObject.SetActive(isReady);
            }

            _inLobby.Show();
        }
        #endregion

        private void OnLobbyStateChanged(string lobbyState)
        {
            if (lobbyState == WAITING_FOR_ALL_READY)
            {

            }
            else if (lobbyState == FINISHED_ALL_READY_TO_PLAY_GAME)
            {
                _gameStarting.Show();
                GameManager.instance.ChangeState(State.StartupGamePlay);
            }

            _lobbyState = lobbyState;
        }

        /// <summary>
        /// 전부 준비되면 방장의 play 버튼 누를수있게
        /// </summary>
        private void CheckAllReady()
        {
            bool isAllReady = true;
            int count = 0;

            // 전체 유저데이터 순회
            foreach (var kv in _controller.userCustomProperties.Values)
            {
                count++;

                bool propertyExist = false;

                // 유저데이터에서 IsReady 데이터 확인
                foreach (var (k, v) in kv)
                {
                    if (k == IS_READY)
                    {
                        propertyExist = true;

                        if (v == bool.FalseString)
                        {
                            isAllReady = false;
                            break;
                        }
                    }
                }

                // 유저데이터에 IsReady가 없었으면 아직 동기화안된 애가 있다.
                if (propertyExist == false)
                { 
                    isAllReady = false;
                    break;
                }
            }

            if (count != _controller.numClient)
                isAllReady = false;

            _inLobbyPlay.interactable = isAllReady;
        }

    }
}
