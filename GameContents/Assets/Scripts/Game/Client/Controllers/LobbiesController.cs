using Game.Client.Network;
using Game.Lobbies;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using static Game.Client.LobbiesConstants;

namespace Game.Client.Controllers
{
    public class LobbiesController : MonoBehaviour
    {
        public bool isMaster
        {
            get
            {
                lock (_gate)
                {
                    if (_cachedLobby == null)
                        return false;

                    IDictionary<string, string> properties = _cachedLobby.GetUserCustomProperties(GrpcConnection.clientInfo.ClientId);

                    if (properties == null)
                        return false;

                    if (properties.TryGetValue(IS_MASTER, out string valueString))
                        return bool.Parse(valueString);

                    return false;
                }
            }
        }

        public IDictionary<string, string> myUsercustomProperties
        {
            get
            {
                return _cachedLobby.GetUserCustomProperties(GrpcConnection.clientInfo.ClientId);
            }
        }

        public IDictionary<int, Dictionary<string, string>> userCustomProperties
        {
            get
            {
                return _cachedLobby.GetAllUserCustomProperties();
            }
        }

        public int numClient
        {
            get
            {
                return _cachedLobby.numClient;
            }
        }

        public IReadOnlyDictionary<int, string> CurrentLobbyUsers
        {
            get
            {
                var users = new Dictionary<int, string>();
                foreach (var userProps in userCustomProperties)
                {
                    if (userProps.Value.TryGetValue(USER_ID, out string userId))
                    {
                        users[userProps.Key] = userId;
                    }
                }
                return users;
            }
        }


        private LobbiesService.LobbiesServiceClient _lobbiesClient;
        private AsyncServerStreamingCall<LobbyEvent> _eventStream;
        private CancellationTokenSource _cts;

        
        /// <summary>
        /// 클라이언트에서 Lobby 정보 캐싱용 
        /// </summary>
        public class LocalLobby
        {
            public bool empty => _lobbyId < 0;

            public int lobbyId
            {
                get { return _lobbyId; }
                set
                {
                    _lobbyId = value;
                    MultiplayMatchBlackboard.lobbyId = value;
                }
            }
            public int hostClientId
            {
                get => _hostClientId;
                set => _hostClientId = value;
            }
            public int maxClient
            {
                get => _maxClient;
                set => _maxClient = value;
            }
            public int numClient
            {
                get => _numClient;
            }

            private int _lobbyId = -1;
            private int _hostClientId = -1;
            private int _maxClient = -1;
            private int _numClient = -1;
            private Dictionary<string, string> _customProperties = new Dictionary<string, string>();
            private Dictionary<int, Dictionary<string, string>> _userCustomProperties = new Dictionary<int, Dictionary<string, string>>();

            public void ApplyLobbyInfo(LobbyInfo info)
            {
                lobbyId = info.LobbyId;
                hostClientId = info.HostClientId;
                maxClient = info.MaxClient;
                _numClient = info.NumClient;

                foreach (var item in info.CustomProperties)
                {
                    SetCustomProperty(item.Key, item.Value);
                }
            }
            
            public void AddClient(int clientId)
            {
                _userCustomProperties[clientId] = new Dictionary<string, string>();
                _numClient++;
            }

            public void RemoveClient(int clientId)
            {
                _userCustomProperties.Remove(clientId);
                _numClient--;
            }

            public void Clear()
            {
                _lobbyId = -1;
                _lobbyId = -1;
                _hostClientId = 1;
                _maxClient = -1;
                _numClient = -1;
                _customProperties.Clear();
                _userCustomProperties.Clear();
            }

            public void SetCustomProperty(string key, string value)
            {
                _customProperties[key] = value;
            }

            public string GetCustomProperty(string key)
            {
                return _customProperties[key];
            }

            public IDictionary<string, string> GetCustomProperties()
            {
                return _customProperties;
            }

            public void SetUserCustomProperty(int clientId, string key, string value)
            {
                if (_userCustomProperties.TryGetValue(clientId, out var properties) == false)
                    properties = _userCustomProperties[clientId] = new Dictionary<string, string>();

                properties[key] = value;
                MultiplayMatchBlackboard.clientIds = _userCustomProperties.Keys;
            }

            public string GetUserCustomProperty(int clinetId, string key)
            {
                if (_userCustomProperties.TryGetValue(clinetId, out var properties) == false)
                    return null;

                if (_userCustomProperties[clinetId].TryGetValue(key, out var property) == false)
                    return null;

                return property;
            }

            public IDictionary<string, string> GetUserCustomProperties(int clientId)
            {
                if (_userCustomProperties.TryGetValue(clientId, out var properties))
                    return properties;

                return null;
            }

            public IDictionary<int, Dictionary<string, string>> GetAllUserCustomProperties()
            {
                return _userCustomProperties;
            }
        }

        private LocalLobby _cachedLobby;
        private readonly object _gate = new object();

        public event Action<int> onMemberJoin;
        public event Action<int> onMemberLeft;
        public event Action<IDictionary<string, string>> onLobbyPropsChanged;
        public event Action<int, IDictionary<string, string>> onUserPropsChanged;


        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _cachedLobby = new LocalLobby();
            _cts = new CancellationTokenSource();
            _lobbiesClient = new LobbiesService.LobbiesServiceClient(GrpcConnection.channel);
        }

        public async Task<(bool success, string message, IList<UserInLobbyInfo>)> CreateLobbyAsync(int maxClient)
        {
            try
            {
                var response = await _lobbiesClient.CreateLobbyAsync(new CreateLobbyRequest
                {
                    HostClientId = GrpcConnection.clientInfo.ClientId,
                    MaxClient = maxClient
                });

                if (response.Success)
                {
                    lock (_gate)
                    {
                        _cachedLobby.ApplyLobbyInfo(response.LobbyInfo);

                        // User 정보 캐싱
                        foreach (var userInfo in response.UserInLobbyInfos)
                        {
                            foreach (var (k, v) in userInfo.CustomProperties)
                            {
                                _cachedLobby.SetUserCustomProperty(userInfo.ClientId, k, v);
                            }
                        }
                    }
                    SubscribeLobby(HandleLobbyEvents);
                }

                return (response.Success, response.Success ? "Lobby Created" : "Failed to create lobby.", response.UserInLobbyInfos );
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool success, string message, IList<UserInLobbyInfo> members)> JoinLobbyAsync(int lobbyId)
        {
            try
            {
                var response = await _lobbiesClient.JoinLobbyAsync(new JoinLobbyRequest
                {
                    LobbyId = lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                });

                if (response.Success)
                {
                    lock (_gate)
                    {
                        _cachedLobby.ApplyLobbyInfo(response.LobbyInfo);

                        // User 정보 캐싱
                        foreach (var userInfo in response.UserInLobbyInfos)
                        {
                            foreach (var (k, v) in userInfo.CustomProperties)
                            {
                                _cachedLobby.SetUserCustomProperty(userInfo.ClientId, k, v);
                            }
                        }
                    }

                    SubscribeLobby(HandleLobbyEvents);
                }

                return (response.Success, response.Success ? "Lobby Joined" : "Failed to join lobby.", response.UserInLobbyInfos);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool success, string message)> LeaveLobbyAsync()
        {
            try
            {
                bool wasMaster = isMaster;

                lock (_gate)
                {
                    if (wasMaster)
                    {
                        int nextMasterId = _cachedLobby.GetAllUserCustomProperties().Keys.FirstOrDefault(clientId => clientId != GrpcConnection.clientInfo.ClientId);

                        if (nextMasterId > 0)
                            _ = ChangeMasterAsync(GrpcConnection.clientInfo.ClientId, nextMasterId);
                    }
                }

                LeaveLobbyResponse response = await _lobbiesClient.LeaveLobbyAsync(new LeaveLobbyRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                });

                if (response.Success)
                {
                    _cachedLobby.Clear();
                    _cts.Cancel();
                    _eventStream?.Dispose();
                    _cts = new CancellationTokenSource();
                    return (true, "Left lobby.");
                }
                else
                {
                    return (false, "Failed to leave lobby.");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task ChangeMasterAsync(int prevMasterClientId, int nextMasterClientId)
        {
            // TODO : 서버데이터 검증 부분 및 각 gRPC Response 예외처리

            await SetUserCustomPropertiesAsync(prevMasterClientId, new Dictionary<string, string>
            {
                { IS_MASTER, bool.FalseString }
            });

            await SetUserCustomPropertiesAsync(nextMasterClientId, new Dictionary<string, string>
            {
                { IS_MASTER, bool.TrueString },
                { IS_READY, bool.TrueString }
            });
        }

        public async Task<(bool success, IList<LobbyInfo>)> GetLobbyListAsync()
        {
            try
            {
                var response = await _lobbiesClient.GetLobbyListAsync(new Empty());
                var lobbyList = response.LobbyInfos.ToList();
                return (true, lobbyList);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get lobbylist {ex.Message}");
                return (false, null);
            }
        }

        public async Task<(bool success, string message)> SetLobbyCustomPropertiesAsync(IDictionary<string, string> properties)
        {
            try
            {
                var response = await _lobbiesClient.SetLobbyCustomPropertiesAsync(new SetLobbyCustomPropertiesRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    Kv = { properties }
                });

                return (response.Success, response.Success ? "Changed lobby properties" : "Failed to change lobby properties");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<(bool success, string message)> SetUserCustomPropertiesAsync(int targetClientId, IDictionary<string, string> properties)
        {
            try
            {
                var response = await _lobbiesClient.SetUserInCustomLobbyPropertiesAsync(new SetUserInLobbyCustomPropertiesRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    ClientId = targetClientId,
                    Kv = { properties }
                });

                return (response.Success, response.Success ? "Changed user properties" : "Failed to change user properties");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Server streaming service 를 쓰기때문에 무한루프형태로 계속 스트림을읽어야함
        /// </summary>
        public void SubscribeLobby(Action<LobbyEvent> onEvent)
        {
            lock (_gate)
            {
                // TODO : lobbyid 및 clientid 예외처리

                _eventStream = _lobbiesClient.SubscribeLobby(new SubscribeLobbyRequest
                {
                    LobbyId = _cachedLobby.lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                }, cancellationToken: _cts.Token);
            }

            // 주의. Task.Run 실행하면 ThreadPool 에서 다른 쓰레드에 이 작업을 할당한다.
            // 즉, 메인쓰레드가 아니므로 여기서 구독한 내용에 Unity Logic 이나 UI 처리 등이 있으면 메인쓰레드 Context 동기화가 필요하다.
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var lobbyEvent in _eventStream.ResponseStream.ReadAllAsync(_cts.Token))
                        onEvent?.Invoke(lobbyEvent);
                }
                catch (Exception ex)
                {
                    // 스트리밍 종료됨
                    Debug.Log(ex.Message);
                }
            }, cancellationToken: _cts.Token);
        }

        async void HandleLobbyEvents(LobbyEvent e)
        {
            lock (_gate)
            {
                switch (e.Type)
                {
                    case LobbyEvent.Types.EventType.MemberJoin:
                        {
                            _cachedLobby.AddClient(e.ClientId);
                            onMemberJoin?.Invoke(e.ClientId);
                        }
                        break;
                    case LobbyEvent.Types.EventType.MemberLeft:
                        {
                            _cachedLobby.RemoveClient(e.ClientId);
                            onMemberLeft?.Invoke(e.ClientId);
                        }
                        break;
                    case LobbyEvent.Types.EventType.LobbyPropChanged:
                        {
                            foreach (var (k, v) in e.Kv)
                            {
                                _cachedLobby.SetCustomProperty(k, v);
                            }

                            onLobbyPropsChanged?.Invoke(e.Kv);
                        }
                        break;
                    case LobbyEvent.Types.EventType.UserPropChanged:
                        {
                            foreach (var (k, v) in e.Kv)
                            {
                                _cachedLobby.SetUserCustomProperty(e.ClientId, k, v);
                            }

                            onUserPropsChanged?.Invoke(e.ClientId, e.Kv);
                        }
                        break;
                    default:
                        throw new ArgumentException($"{e.Type} event does not implemented.");
                }
            }
        }
    }
}