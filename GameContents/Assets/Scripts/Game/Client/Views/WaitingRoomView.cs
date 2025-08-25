using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Game.Client.Controllers;
using Game.Client.Network;
using Game.Lobbies;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using Utils;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Views
{
    public class WaitingRoomView : MonoBehaviour
    {
        [Header("Controllers")]
        [SerializeField] LobbiesController _lobbiesController; // 비어도 됨(확인 중)

        [Header("Canvas - PlayerCanvas")]
        [SerializeField] Transform _inLobbyContent;
        [SerializeField] Button _readyButton;
        UserInLobbyInfoSlot _userInLobbySlot;
        List<UserInLobbyInfoSlot> _userInLobbySlots;
        string _lobbyState;

        [Header("Canvas - StageBtnCanvas")]

        [Header("Canvas - StageDetailCanvas")]
        [SerializeField] Button _startButton;

        [Header("Canvas - LeaveCanvas")]
        [SerializeField] Button _leaveRoom;

        [Header("Canvas - Loading")]
        [SerializeField] Canvas _loading;

        [Header("Canvas - GameStarting")]
        [SerializeField] Canvas _gameStarting;

        bool isQuit = false;

        private void Awake()
        {
            //_loading.Hide(); 일단 중지
            _gameStarting.Hide();

            _userInLobbySlot = _inLobbyContent.GetChild(0).GetComponent<UserInLobbyInfoSlot>();
            _userInLobbySlot.gameObject.SetActive(false);
            _userInLobbySlots = new List<UserInLobbyInfoSlot>(4); // 이 게임은 4명을 초과하는 인게임 컨텐츠가없다. 이정도로 적은 데이터는 선형 O(N)탐색이 Hash O(1) 탐색보다 저렴하다.
        }

        private void ResolveController()
        {
            if (_lobbiesController == null) _lobbiesController = LobbiesController.Instance;
            if (_lobbiesController == null) { Debug.LogError("LobbiesController missing"); enabled = false; }
        }

        private void OnEnable()
        {
            ResolveController();

            _readyButton.onClick.AddListener(OnInLobbyReadyButtonClicked);
            _leaveRoom.onClick.AddListener(OnInLobbyLeaveButtonClicked);
            _startButton.onClick.AddListener(OnInLobbyPlayButtonClicked);
            _lobbiesController.onMemberJoin += OnMemberJoin;
            _lobbiesController.onMemberLeft += OnMemberLeft;
            _lobbiesController.onLobbyPropsChanged += OnLobbyPropsChanged;
            _lobbiesController.onUserPropsChanged += OnUserPropsChanged;

            // 현재 로비 멤버들로 초기화
            InitializeCurrentLobbyMembers();
        }

        private void OnDisable()
        {

            _readyButton.onClick.RemoveListener(OnInLobbyReadyButtonClicked);
            _leaveRoom.onClick.RemoveListener(OnInLobbyLeaveButtonClicked);
            _startButton.onClick.RemoveListener(OnInLobbyPlayButtonClicked);

            if (_lobbiesController == null) return; // 컨트롤러가 없으면 이벤트 해제 불필요
            _lobbiesController.onMemberJoin -= OnMemberJoin;
            _lobbiesController.onMemberLeft -= OnMemberLeft;
            _lobbiesController.onLobbyPropsChanged -= OnLobbyPropsChanged;
            _lobbiesController.onUserPropsChanged -= OnUserPropsChanged;
        }

        private void OnApplicationQuit()
        {
            isQuit = true;
            OnInLobbyLeaveButtonClicked();
        }

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
                var slot = Instantiate(_userInLobbySlot, _inLobbyContent, false);
                slot.Init(_lobbiesController);
                slot.Refresh(info);
                slot.gameObject.SetActive(true);
                _userInLobbySlots.Add(slot);
            }
        }
        private async void OnInLobbyReadyButtonClicked() // 필요 없음?
        {
            /*if (_lobbiesController.isMaster)
                return; // 방장은 Ready 상태를 변경할 수 없음

            _readyButton.interactable = false; // 처리 중 클릭 방지

            // 기존 CustomProperties를 가져와서 병합 (덮어쓰기 방지)
            var existingProps = new Dictionary<string, string>();
            if (_lobbiesController.userCustomProperties.TryGetValue(GrpcConnection.clientInfo.ClientId, out var currentProps))
            {
                // 기존 Properties 복사
                foreach (var prop in currentProps)
                {
                    existingProps[prop.Key] = prop.Value;
                }
            }

            if (existingProps[IS_READY] == bool.TrueString)
            {
                // 이미 준비 상태인 경우, 취소
                existingProps[IS_READY] = bool.FalseString;
                _readyButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Ready"; // 버튼 텍스트 변경
                _readyButton.targetGraphic.color = Color.red; // 버튼 색상 변경
            }
            else
            {
                // 준비 상태로 변경
                existingProps[IS_READY] = bool.TrueString;
                _readyButton.GetComponentInChildren<TMPro.TMP_Text>().text = "Cancel"; // 버튼 텍스트 변경
                _readyButton.targetGraphic.color = Color.green; // 버튼 색상 변경
            }
            // IsReady만 업데이트 (기존 데이터 유지)
            var response = await _lobbiesController.SetUserCustomPropertiesAsync(GrpcConnection.clientInfo.ClientId, existingProps);

            _readyButton.interactable = true; // 처리 완료 후 클릭 가능*/
        }

        private async void OnInLobbyLeaveButtonClicked()
        {
            var (success, message) = await _lobbiesController.LeaveLobbyAsync();

            if (isQuit)
                return;

            if (success)
            {
                // *** 변경: 캔버스 전환 대신 로비 리스트 씬으로 이동 ***
                StartCoroutine(SceneTransitionUtility.C_LoadAndSwitchAsync("Lobbies"));
                return;
            }
            else
            {
                Console.WriteLine($"Failed to leave lobby: {message}");
            }
        }

        private async void OnInLobbyPlayButtonClicked()
        {
            var (success, message) = await _lobbiesController.SetLobbyCustomPropertiesAsync(new Dictionary<string, string>
            {
                { LOBBY_STATE, FINISHED_ALL_READY_TO_PLAY_GAME }
            });

            if (success)
            {
                MultiplayMatchBlackboard.isMaster = true;

                MultiplayMatchBlackboard.lobbyUsers = new Dictionary<int, string>(_lobbiesController.CurrentLobbyUsers);

                // Nothing to do... 서버 할당되어서 이벤트 처리될때까지 그냥 기다림.
            }
            else
            {
                Console.WriteLine($"Failed to start : {message}");
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
            slot.Init(_lobbiesController);
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

            // 멤버가 나간 후 남은 멤버들의 CustomProperties가 손실될 수 있으므로 재확인 및 복구
            await RefreshRemainingMembersAsync();
        }

        private async void OnUserPropsChanged(int clientId, IDictionary<string, string> props)
        {
            await Awaitable.MainThreadAsync();

            Debug.Log("OnUserPropsChanged");

            // 함수 시작 부분에 추가 - 디버깅을 위한 로그
            Debug.Log($"OnUserPropsChanged - ClientId: {clientId}, Props: {string.Join(", ", props.Select(kv => $"{kv.Key}={kv.Value}"))}");

            int slotIndex = _userInLobbySlots.FindIndex(slot => slot.clientId == clientId);

            if (slotIndex < 0)
                return;

            // *** 여기에 추가 ***
            // 방장 변경 시 UserId가 없으면 자동으로 재설정
            if (clientId == GrpcConnection.clientInfo.ClientId &&
                props.ContainsKey(IS_MASTER) &&
                props[IS_MASTER] == bool.TrueString &&
                !props.ContainsKey(USER_ID))
            {
                Debug.LogWarning("Became master but USER_ID missing. Restoring USER_ID...");

                // 기존 Properties와 새로운 Properties 병합
                var mergedProps = new Dictionary<string, string>();
                if (_lobbiesController.userCustomProperties.TryGetValue(clientId, out var existingProps))
                {
                    foreach (var prop in existingProps)
                    {
                        mergedProps[prop.Key] = prop.Value;
                    }
                }

                // 새로운 Properties 추가
                foreach (var prop in props)
                {
                    mergedProps[prop.Key] = prop.Value;
                }

                // USER_ID가 없으면 추가
                if (!mergedProps.ContainsKey(USER_ID))
                {
                    mergedProps[USER_ID] = GrpcConnection.clientInfo.UserId;
                }

                // 서버에 전체 Properties 재설정
                await _lobbiesController.SetUserCustomPropertiesAsync(clientId, mergedProps);
                return; // 재설정 후 다시 이벤트가 올 것이므로 여기서 종료
            }

            _userInLobbySlots[slotIndex].Refresh(new UserInLobbyInfo
            {
                ClientId = clientId,
                CustomProperties = { props }
            });

            if (props.ContainsKey(USER_ID))
            {
                // UserId 가 동기화되었으면 이제 슬롯 활성화
                _userInLobbySlots[slotIndex].gameObject.SetActive(true);
                Debug.Log($"Slot activated for clientId: {clientId}"); // 슬롯이 정상적으로 활성화되었는지 확인
            }

            if (clientId == GrpcConnection.clientInfo.ClientId)
            {
                RefreshInLobbyUIs();
            }

            if (_lobbiesController.isMaster)
                CheckAllReady();
        }

        private void RefreshInLobbyUIs()
        {
            bool isMaster = _lobbiesController.isMaster;

            if (isMaster)
            {
                // 방장은 항상 Ready 상태, 초록색, 클릭 불가능
                _startButton.gameObject.SetActive(true);
            }
            else
            {
                // 일반 플레이어
                _startButton.gameObject.SetActive(false);

                /*// 현재 Ready 상태 확인 // 필요 없음?
                bool isReady = false;
                if (_lobbiesController.myUsercustomProperties.TryGetValue(IS_READY, out string isReadyString))
                    isReady = bool.Parse(isReadyString);*/
            }
        }

        private async void OnLobbyPropsChanged(IDictionary<string, string> props)
        {
            await Awaitable.MainThreadAsync();

            Debug.Log("OnLobbyPropsChanged");
            // TODO : 로비 제목 변경, 최대인원수 변경 같은거 처리

            if (props.TryGetValue(LOBBY_STATE, out string newLobbyState))
                OnLobbyStateChanged(newLobbyState);
        }

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
            foreach (var kv in _lobbiesController.userCustomProperties.Values)
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

            if (count != _lobbiesController.numClient)
                isAllReady = false;

            _startButton.interactable = isAllReady;
        }

        /// <summary>
        /// 멤버가 나간 후 남은 멤버들의 CustomProperties를 재확인하고 복구
        /// 서버에서 멤버 제거 시 다른 멤버들의 데이터가 초기화될 수 있는 문제 해결
        /// </summary>
        private async Task RefreshRemainingMembersAsync()
        {
            await Task.Delay(500);

            foreach (var slot in _userInLobbySlots)
            {
                if (_lobbiesController.userCustomProperties.TryGetValue(slot.clientId, out var props))
                {
                    if (!props.ContainsKey(USER_ID) && slot.clientId == GrpcConnection.clientInfo.ClientId)
                    {
                        await _lobbiesController.SetUserCustomPropertiesAsync(slot.clientId, new Dictionary<string, string>
                {
                    { USER_ID, GrpcConnection.clientInfo.UserId }
                });
                    }
                }
            }
        }

        private void InitializeCurrentLobbyMembers()
        {
            /*// 현재 로비에 있는 멤버들의 슬롯 생성
            foreach (var kvp in _lobbiesController.userCustomProperties)
            {
                RefreshUserInLobbyContent(new List<UserInLobbyInfo> {
                new UserInLobbyInfo { ClientId = kvp.Key, CustomProperties = { kvp.Value } }
            });
            }
            RefreshInLobbyUIs();*/

            var list = new List<UserInLobbyInfo>();
            foreach (var kvp in _lobbiesController.userCustomProperties)
                list.Add(new UserInLobbyInfo { ClientId = kvp.Key, CustomProperties = { kvp.Value } });

            RefreshUserInLobbyContent(list);   // 한 번에!
            RefreshInLobbyUIs();
        }
    }
}
