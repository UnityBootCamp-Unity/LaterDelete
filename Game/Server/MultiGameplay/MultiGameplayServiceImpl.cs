using Game.Multigameplay.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Game.Server.MultiGameplay
{
    class MultiGameplayServiceImpl : MultiGamePlayService.MultiGamePlayServiceBase
    {
        public MultiGameplayServiceImpl(ILogger<MultiGameplayServiceImpl> logger, MultiGameServerManager serverManager) 
        {
            _logger = logger;
            _serverManager = serverManager;
        }

        private readonly ILogger<MultiGameplayServiceImpl> _logger;
        private readonly MultiGameServerManager _serverManager;


        public override async Task<CreateAllocationResponse> CreateAllocation(CreateAllocationRequest request, ServerCallContext context)
        {
            var payload = JsonConvert.DeserializeObject<UnityMultiplayerGameServerHostingFacade.AllocationPayload>(request.Payload);
            _logger.LogInformation($"Request queue an allocation for lobby {payload.lobbyId}");

            try
            {
                var (allocationId, href) = await UnityMultiplayerGameServerHostingFacade.CreateAllocationAsync
                    (
                        request.AllocationId,
                        request.BuildConfigurationId,
                        request.RegionId,
                        request.Restart,
                        payload
                    );

                if (string.IsNullOrEmpty(allocationId))
                    throw new Exception("Failed to allocate server.");

                // TODO : 임시로 Polling get 하는중. href 로 allocation 확인하는 코드로 바꿔야함
                UnityMultiplayerGameServerHostingFacade.ServerAllocation allocation = null;
                int pollingCount = 0;

                while (pollingCount < 3)
                {
                    await Task.Delay(5000);
                    try
                    {
                        allocation = await UnityMultiplayerGameServerHostingFacade.GetAllocationAsync(allocationId);
                        
                        if (allocation.IsReady)
                            break;
                    }
                    catch
                    {
                        pollingCount++;
                    }
                }

                if (pollingCount >= 3)
                    throw new Exception("Failed to get allocation");

                string matchId = allocationId; //Guid.NewGuid().ToString(); 일단 임시로 allocatonId 를 matchId 로 쓸거임. 

                await _serverManager.RegisterMatchAsync(new MultiGameServerManager.MatchInfo
                {
                    MatchId = matchId,
                    ServerId = allocation.ServerId,
                    ServerIp = allocation.IpAddress,
                    ServerPort = allocation.Port,
                    LobbyId = payload.lobbyId,
                    ClientIds = payload.clientIds,
                });

                // 모든 클라이언트가 gRPC 이벤트 구독을 할때까지 기다림 (임시방편..)
                // TODO : 다른클라이언트가 구독할때 이벤트로받아서 구독중인 클라이언트목록을 클라이언트가 받을수있게..
                while (true)
                {
                    await Task.Delay(2000);
                    
                    int subscriberCount = _serverManager.GetSubscriberCount(payload.lobbyId);

                    if (subscriberCount == payload.clientIds.Count)
                        break;
                }

                var allocationInfo = new AllocationInfo
                {
                    AllocationId = allocationId,
                    BuildConfigurationId = allocation.BuildConfigurationId,
                    FleetId = allocation.FleetId,
                    IpAddress = allocation.IpAddress,
                    GamePort = allocation.Port,
                    MachineId = allocation.MachineId,
                    IsReady = allocation.IsReady,
                    RegionId = allocation.Region,
                    ServerId = allocation.ServerId,
                };

                // 모든 클라이언트에게 이벤트 뿌림
                await _serverManager.BroadcastAllocationEventAsync(payload.lobbyId, new AllocationEvent
                {
                    Type = AllocationEvent.Types.EventType.AllocationReady,
                    AllocationId = allocationId,
                    LobbyId = payload.lobbyId,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                    Allocation = allocationInfo,
                });

                return new CreateAllocationResponse
                {
                    Allocation = allocationInfo,
                    Href = string.Empty
                };
            }
            catch (Exception ex)
            {
                await _serverManager.BroadcastAllocationEventAsync(payload.lobbyId, new AllocationEvent
                {
                    Type = AllocationEvent.Types.EventType.AllocationFailed,
                    ErrorMessage = ex.Message,
                    Timestamp = Timestamp.FromDateTime(DateTime.UtcNow)
                });

                return new CreateAllocationResponse
                {
                    Allocation = new AllocationInfo
                    {
                        AllocationId = ex.Message,
                        BuildConfigurationId = -1,
                        FleetId = "-1",
                        IpAddress = "-1",
                        GamePort = 0,
                        MachineId = 0,
                        IsReady = false,
                        RegionId = "-1",
                        ServerId = -1,
                    },
                    Href = string.Empty
                };
            }
        }

        public override async Task<Empty> DeleteAllocation(DeleteAllocationRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request deallocate {request.AllocationId}");

            try
            {
                await UnityMultiplayerGameServerHostingFacade.DeleteAllocationAsync(request.AllocationId);
                _logger.LogInformation($"Deallocated server {request.AllocationId}");
                // TODO : broadcast ths event to all clients.
                return new Empty();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to deallocate server {request.AllocationId}, {ex.Message}");
                return new Empty();
            }
        }

        public override async Task<GetAllocationResponse> GetAllocation(GetAllocationRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request get allocation {request.AllocationId}");

            try
            {
                var allocation = await UnityMultiplayerGameServerHostingFacade.GetAllocationAsync(request.AllocationId);

                if (allocation == null)
                {
                    throw new RpcException(new Status(StatusCode.NotFound, "Allocation not found"));
                }

                return new GetAllocationResponse
                {
                    Allocation = new AllocationInfo
                    {
                        AllocationId = allocation.AllocationId,
                        BuildConfigurationId = allocation.BuildConfigurationId,
                        FleetId = allocation.FleetId,
                        RegionId = allocation.Region,
                        ServerId = allocation.ServerId,
                        MachineId = allocation.MachineId,
                        IpAddress = allocation.IpAddress,
                        GamePort = allocation.Port,
                        IsReady = allocation.IsReady,
                    }
                };
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<GetAllocationsResponse> GetAllocations(GetAllocationsRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request get allocations.");

            try
            {
                var allocations = await UnityMultiplayerGameServerHostingFacade.GetAllocationsAsync
                    (
                        request.Age, 
                        request.Limit > 0 ? request.Limit : null,
                        request.Offset > 0 ? request.Offset : null,
                        request.AllocationIds.Count > 0 ? request.AllocationIds : null
                    );

                List<AllocationInfo> allocationInfos = new List<AllocationInfo>();

                foreach (var allocation in allocations)
                {
                    allocationInfos.Add(new AllocationInfo
                    {
                        AllocationId = allocation.AllocationId,
                        BuildConfigurationId = allocation.BuildConfigurationId,
                        FleetId = allocation.FleetId,
                        RegionId = allocation.Region,
                        ServerId = allocation.ServerId,
                        MachineId = allocation.MachineId,
                        IpAddress = allocation.IpAddress,
                        GamePort = allocation.Port,
                        IsReady = allocation.IsReady,
                    });
                }

                return new GetAllocationsResponse
                {
                    Allocations = { allocationInfos },
                    Pagination = new PaginationInfo
                    {
                        Limit = request.Limit,
                        Offset = request.Offset,
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get allocations");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task<Empty> UpdateGameplayStatus(UpdateGameplayStatusRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"Request update gameplay status {request.AllocationId}.");

            try 
            {
                var currentStatus = _serverManager.GetServerStatus(request.LobbyId);

                await _serverManager.UpdateServerStatusAsync(request.LobbyId, new MultiGameServerManager.ServerStatusInfo
                {
                    Status = request.Status,
                    TotalPlayers = currentStatus.TotalPlayers,
                    MaxPlayers = currentStatus.MaxPlayers,
                });

                await _serverManager.BroadcastAllocationEventAsync(request.LobbyId, new AllocationEvent
                {
                    Type = AllocationEvent.Types.EventType.AllocationStatusChanged,
                    LobbyId = request.LobbyId,
                    NewStatus = request.Status,
                });

                // 활동 갱신(무인 타이머 해제/리셋에 사용)
                _serverManager.TouchLobbyActivity(request.LobbyId);

                return new Empty();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to update game play status of lobby {request.LobbyId}");
                throw new RpcException(new Status(StatusCode.Internal, ex.Message));
            }
        }

        public override async Task SubscribeAllocationEvents(SubscribeAllocationEventsRequest request, IServerStreamWriter<AllocationEvent> responseStream, ServerCallContext context)
        {
            _logger.LogInformation($"Client {request.ClientId} subscribing to allocation event for lobby {request.LobbyId}");

            _serverManager.AddAllocationEventStream(request.LobbyId, request.ClientId, responseStream);
            // 스트림 추가 훅
            _serverManager.OnStreamAdded(request.LobbyId, request.ClientId);


            try
            {
                await Task.Delay(Timeout.Infinite, context.CancellationToken); // 무한대기
            }
            catch (OperationCanceledException opCancelledEx)
            {
                _logger.LogInformation($"Subscribe multigameplay cancelled (client {request.ClientId} disconnected");
            }
            finally
            {
                _serverManager.RemoveAllocationEventStream(request.LobbyId, request.ClientId);

                // 스트림 제거 훅(Grace 진입 + 타이머 무장 가능)
                _serverManager.OnStreamRemoved(request.LobbyId, request.ClientId);

                _logger.LogInformation($"Cleaned up of client {request.ClientId} multigameplay subscription.");
            }
        }

        // ===== 신규: ResumeSession 구현 =====
        public override async Task<ResumeSessionResponse> ResumeSession(
     ResumeSessionRequest request, ServerCallContext context)
        {
            var (ok, resumed, token, match, status, reason) =
                _serverManager.ResumeOrIssueSession(request.LobbyId, request.ClientId, request.SessionToken ?? "");

            AllocationInfo allocationInfo = null;

            if (match != null && !string.IsNullOrEmpty(match.MatchId))
            {
                try
                {
                    // ✅ UGS에서 최신 할당 정보 재조회해서 정확히 채운다
                    var alloc = await UnityMultiplayerGameServerHostingFacade.GetAllocationAsync(match.MatchId);

                    allocationInfo = new AllocationInfo
                    {
                        AllocationId = alloc.AllocationId,
                        ServerId = alloc.ServerId,
                        IpAddress = alloc.IpAddress,
                        GamePort = alloc.Port,                       // ulong
                        RegionId = alloc.Region,                     // ex) "ap-northeast3"
                        FleetId = alloc.FleetId,
                        BuildConfigurationId = alloc.BuildConfigurationId,
                        MachineId = alloc.MachineId,
                        IsReady = alloc.IsReady
                    };
                }
                catch (Exception ex)
                {
                    // 👉 조회 실패 시, 최소 필드만 채워 응답 (옵션 A로 폴백)
                    _logger.LogWarning(ex, $"ResumeSession: GetAllocationAsync failed for {match.MatchId}. Fallback to minimal info.");
                    allocationInfo = new AllocationInfo
                    {
                        AllocationId = match.MatchId,
                        ServerId = match.ServerId,
                        IpAddress = match.ServerIp,
                        GamePort = match.ServerPort,
                        IsReady = true
                    };
                }
            }

            return new ResumeSessionResponse
            {
                Ok = ok,
                Resumed = resumed,
                SessionToken = token ?? "",
                Allocation = allocationInfo,
                Status = status?.Status ?? GameplayStatus.Unknown,
                ServerTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow),
                Reason = reason ?? ""
            };
        }

    }
}
