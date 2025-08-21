using Game.Lobbies;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using static Server.Lobbies.LobbiesManager;

namespace Server.Lobbies
{
    class LobbiesServiceImpl : LobbiesService.LobbiesServiceBase
    {
        public LobbiesServiceImpl(ILogger<LobbiesServiceImpl> logger, LobbiesManager manager)
        {
            _logger = logger;
            _manager = manager;
        }

        ILogger<LobbiesServiceImpl> _logger;
        LobbiesManager _manager;

        public override async Task<CreateLobbyResponse> CreateLobby(CreateLobbyRequest request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin CreateLobby");
                int lobbyId = _manager.Create(request.HostClientId, request.MaxClient);

                bool success = lobbyId >= 0;

                if (success)
                {
                    return new CreateLobbyResponse
                    {
                        Success = success,
                        LobbyInfo = _manager.ToLobbyInfo(lobbyId),
                        UserInLobbyInfos = { _manager.ToUserInLobbyInfo(lobbyId, request.HostClientId) }
                    };
                }
                else
                {
                    return new CreateLobbyResponse
                    {
                        Success = false,
                        LobbyInfo = new LobbyInfo
                        {
                            LobbyId = -1,
                            HostClientId = request.HostClientId,
                            MaxClient = -1,
                            NumClient = -1,
                            CustomProperties = { },
                        },
                        UserInLobbyInfos = { }
                    };
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError(ex, "Failed to create lobby");
                return new CreateLobbyResponse
                {
                    Success = false,
                    LobbyInfo = new LobbyInfo
                    {
                        LobbyId = -1,
                        HostClientId = request.HostClientId,
                        MaxClient = -1,
                        NumClient = -1,
                        CustomProperties = { },
                    },
                    UserInLobbyInfos = { }
                };
            }
        }

        public override async Task<JoinLobbyResponse> JoinLobby(JoinLobbyRequest request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin JoinLobby");

                bool ok = _manager.Join(request.LobbyId, request.ClientId);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.MemberJoin,
                            LobbyId = lobby.Id,
                            ClientId = request.ClientId,
                            Kv = { new Dictionary<string, string>() },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });
                        
                        var response = new JoinLobbyResponse
                        {
                            Success = ok,
                            LobbyInfo = _manager.ToLobbyInfo(lobby),
                            UserInLobbyInfos = { lobby.Members.Select(cid => _manager.ToUserInLobbyInfo(lobby, cid)) }
                        };

                        return response;
                    }
                }

                return new JoinLobbyResponse { Success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Join lobby error");
                return new JoinLobbyResponse { Success = false };
            }
        }

        public override async Task<GetLobbyListResponse> GetLobbyList(Empty request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin GetLobbyList");

                return new GetLobbyListResponse
                {
                    LobbyInfos = { _manager.All.Select(_manager.ToLobbyInfo) }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Get lobby list error");

                return new GetLobbyListResponse 
                { 
                    LobbyInfos = { new List<LobbyInfo>() }
                };
            }
        }

        public override async Task<LeaveLobbyResponse> LeaveLobby(LeaveLobbyRequest request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin LeaveLobby");

                bool ok = _manager.Leave(request.LobbyId, request.ClientId);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.MemberLeft,
                            LobbyId = lobby.Id,
                            ClientId = request.ClientId,
                            Kv = { new Dictionary<string, string>() },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });
                    }

                    return new LeaveLobbyResponse
                    {
                        Success = true
                    };
                }

                return new LeaveLobbyResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Leave lobby error.");

                return new LeaveLobbyResponse
                {
                    Success = false
                };
            }
        }

        public override async Task<SetLobbyCustomPropertiesResponse> SetLobbyCustomProperties(SetLobbyCustomPropertiesRequest request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin SetLobbyCustomProperties");

                bool ok = _manager.SetLobbyCustomProperties(request.LobbyId, request.Kv);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.LobbyPropChanged,
                            LobbyId = lobby.Id,
                            ClientId = -1,
                            Kv = { request.Kv },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        return new SetLobbyCustomPropertiesResponse
                        {
                            Success = true
                        };
                    }
                }

                return new SetLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set lobby custom properties error.");

                return new SetLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
        }

        public override async Task<SetUserInLobbyCustomPropertiesResponse> SetUserInCustomLobbyProperties(SetUserInLobbyCustomPropertiesRequest request, ServerCallContext context)
        {
            try
            {
                _logger.Log(LogLevel.Information, "Begin SetUserInCustomLobbyProperties");

                bool ok = _manager.SetUserCustomProperties(request.LobbyId, request.ClientId, request.Kv);

                if (ok)
                {
                    if (_manager.TryGetLobby(request.LobbyId, out var lobby))
                    {
                        await _manager.Broadcast(lobby.Id, new LobbyEvent
                        {
                            Type = LobbyEvent.Types.EventType.UserPropChanged,
                            LobbyId = lobby.Id,
                            ClientId = request.ClientId,
                            Kv = { request.Kv },
                            Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                        });

                        return new SetUserInLobbyCustomPropertiesResponse
                        {
                            Success = true
                        };
                    }
                }

                return new SetUserInLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Set user in lobby custom properties error.");

                return new SetUserInLobbyCustomPropertiesResponse
                {
                    Success = false
                };
            }
        }


        /// <summary>
        /// Streaming rpc 가 종료되면 Client 에 event 송신용 스트림이 닫히기때문에 무한루프를 걸어놔야함.
        /// </summary>
        public override async Task SubscribeLobby(SubscribeLobbyRequest request, IServerStreamWriter<LobbyEvent> responseStream, ServerCallContext context)
        {
            _logger.Log(LogLevel.Information, "Begin SubscribeLobby");
            _manager.AddLobbyEventStream(request.LobbyId, responseStream);

            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken); // 무한대기
            }
            catch (OperationCanceledException opCancelledEx)
            {
                _logger.LogInformation($"SubscribeLobby cancelled (client {request.ClientId} disconnected");
            }
            finally
            {
                _manager.RemoveLobbyEventStream(request.LobbyId, responseStream);
                _manager.Leave(request.LobbyId, request.ClientId);
                _logger.LogInformation($"Cleaned up of client {request.ClientId} lobby subscription.");
            }
        }
    }
}
