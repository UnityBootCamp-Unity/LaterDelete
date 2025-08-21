using Game.Client.GameObjects.Characters;
using Game.Client.Models;
using Game.Client.Network;
using Game.Multigameplay.V1;
using Grpc.Core;
using Newtonsoft.Json;
using NUnit.Framework.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Client.Controllers
{
    public class MultigameplayController : MonoBehaviour
    {
        public AllocationInfo currentAllocation { get; private set; }
        public string currentMatchId { get; private set; }
        public GameplayStatus currentStatus { get; private set; }

        [SerializeField] Player _playerPrefab;
        private Player _player;
        private MultiGamePlayService.MultiGamePlayServiceClient _multiGameplayClient;
        private MultiplaySettings _multiplaySettings;
        private AsyncServerStreamingCall<AllocationEvent> _eventStream;
        private CancellationTokenSource _cts;

        // [ADDED] Ready 대기 시스템
        private TaskCompletionSource<bool> _readyTcs;
        private bool _isWaitingForReady = false;

        public event Action<AllocationInfo> onAllocationCreated;
        public event Action<AllocationInfo> onAllocationReady;
        public event Action onAllocatonDeleted;
        public event Action<string> onAllocationFailed;
        public event Action<GameplayStatus> onAllocationGameplayStatusChanged;

        // -- 신규 --
        private CancellationTokenSource _eventLoopCts;

        public record AllocationPayload
        {
            public int lobbyId { get; set; }
            public List<int> clientIds { get; set; }
            public Dictionary<string, string> gameSettings { get; set; }
        }

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            Debug.Log("=== MultigameplayController Initialize Started ===");

            _multiGameplayClient = new MultiGamePlayService.MultiGamePlayServiceClient(GrpcConnection.channel);
#if TEST_ALPHA
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Alpha");
#elif TEST_BETA
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Beta");
#else
            _multiplaySettings = Resources.Load<MultiplaySettings>("Network/MultiplaySettings_Release");
#endif

            Debug.Log($"MultiplaySettings loaded: {_multiplaySettings?.name}");

            // Resume -> 구독 루프 시작 -> (마스터면) Allocate 후 상태 갱신
            await ResumeOrIssueSessionAsync();

            SubscribeToAllocationEvents();

            Debug.Log($"isMaster: {MultiplayMatchBlackboard.isMaster}");

            // TODO : 모든 Client 가 구독 완료될때까지 기다림
            await Task.Delay(500); // 이벤트 구독 여유시간

            if (MultiplayMatchBlackboard.isMaster)
            {
                Debug.Log("=== Starting Allocation Process (Master) ===");

                var allocateResponse = await AllocateAsync(); // 클라이언트가 직접 Allocation 요청하는로직보다는 서버가 상태 확인하면서 알아서 처리하는게 보안상 나음

                Debug.Log($"AllocateAsync result: success={allocateResponse.success}, message={allocateResponse.message}");

                if (!allocateResponse.success)
                {
                    Debug.LogError($"Allocation failed: {allocateResponse.message}");
                    throw new Exception(allocateResponse.message); // TODO : 재시도 및 예외처리, 로비복귀 등 해야함
                }

                // [ADDED] Ready 이벤트 대기 - AllocationAsync가 성공했지만 아직 Ready가 아닐 수 있음
                if (!IsAllocationReady())
                {
                    Debug.Log("=== Waiting for AllocationReady event (3 minutes timeout) ===");
                    bool ready = await WaitForReadyAsync(TimeSpan.FromMinutes(3));

                    if (!ready)
                    {
                        Debug.LogError("Allocation did not become ready - starting cleanup");
                        await DeallocateAsync();
                        throw new Exception("Allocation did not become ready in time.");
                    }
                }

                Debug.Log("=== Allocation Ready! Starting game... ===");
                await UpdateStatusAsync(GameplayStatus.Starting);
            }
            else
            {
                Debug.Log("=== Non-Master Client - Waiting for allocation events ===");
            }
        }

        // [ADDED] 현재 할당이 Ready 상태인지 확인
        private bool IsAllocationReady()
        {
            return currentAllocation != null && currentAllocation.ServerId > 0;
        }

        // [ADDED] AllocationReady 이벤트를 기다리는 메서드
        private async Task<bool> WaitForReadyAsync(TimeSpan timeout)
        {
            Debug.Log($"=== WaitForReadyAsync started with timeout: {timeout.TotalSeconds} seconds ===");

            _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _isWaitingForReady = true;

            Debug.Log("TaskCompletionSource created and waiting...");

            using var timeoutCts = new CancellationTokenSource(timeout);
            using (timeoutCts.Token.Register(() => {
                Debug.Log("Timeout reached - cancelling wait");
                _readyTcs?.TrySetCanceled();
            }))
            {
                try
                {
                    Debug.Log("Starting to wait for _readyTcs.Task...");
                    await _readyTcs.Task;
                    Debug.Log("_readyTcs.Task completed successfully!");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    Debug.LogError("WaitForReadyAsync cancelled (timeout)");
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"WaitForReadyAsync failed with exception: {ex.Message}");
                    return false;
                }
                finally
                {
                    _isWaitingForReady = false;
                }
            }
        }

        private async void OnApplicationQuit()
        {
            Debug.Log("=== Application Quit - Cleaning up ===");

            if (MultiplayMatchBlackboard.isMaster && !string.IsNullOrEmpty(currentMatchId))
            {
                try
                {
                    await _multiGameplayClient.DeleteAllocationAsync(new DeleteAllocationRequest
                    {
                        AllocationId = currentMatchId,
                    });
                    Debug.Log("Allocation cleanup completed");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to cleanup allocation: {ex.Message}");
                }
            }

            _cts?.Cancel();
            _eventLoopCts?.Cancel();
        }

        private async Task ResumeOrIssueSessionAsync()
        {
            try
            {
                Debug.Log("=== ResumeOrIssueSessionAsync started ===");

                var resp = await _multiGameplayClient.ResumeSessionAsync(new ResumeSessionRequest
                {
                    LobbyId = MultiplayMatchBlackboard.lobbyId,
                    ClientId = GrpcConnection.clientInfo.ClientId,
                    SessionToken = MultiplayMatchBlackboard.sessionToken ?? ""
                });

                Debug.Log($"Resume result: Ok={resp.Ok}, Reason={resp.Reason}");

                if (!resp.Ok)
                {
                    Debug.LogWarning($"Resume failed: {resp.Reason}");
                    // 신규 발급도 실패한 경우. 필요 시 UI 표기.
                }
                else
                {
                    MultiplayMatchBlackboard.sessionToken = resp.SessionToken;
                    if (resp.Allocation != null && resp.Allocation.ServerId > 0)
                    {
                        Debug.Log($"Resumed with existing allocation: {resp.Allocation.AllocationId}, ServerId: {resp.Allocation.ServerId}");
                        currentAllocation = resp.Allocation;
                        currentMatchId = resp.Allocation.AllocationId;
                    }
                    currentStatus = resp.Status;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Resume error: {ex}");
            }
        }

        private void StartEventSubscriptionLoop()
        {
            _eventLoopCts?.Cancel();
            _eventLoopCts = new CancellationTokenSource();

            _ = Task.Run(async () =>
            {
                while (!_eventLoopCts.IsCancellationRequested)
                {
                    try
                    {
                        // 구독 시작
                        _cts = new CancellationTokenSource();
                        _eventStream = _multiGameplayClient.SubscribeAllocationEvents(new SubscribeAllocationEventsRequest
                        {
                            ClientId = GrpcConnection.clientInfo.ClientId,
                            LobbyId = MultiplayMatchBlackboard.lobbyId,
                        }, cancellationToken: _cts.Token);

                        await foreach (var e in _eventStream.ResponseStream.ReadAllAsync(_cts.Token))
                        {
                            HandleAllocationEvent(e);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // 정상 취소
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Allocation event stream error: {ex.Message}");
                    }

                    if (_eventLoopCts.IsCancellationRequested) break;

                    // 끊겼다면 1) Resume 재시도 -> 2) 잠시 대기 후 재구독
                    await ResumeOrIssueSessionAsync();
                    await Task.Delay(2000);
                }
            }, _eventLoopCts.Token);
        }

        private async void OnPlayerStatusChanged(PlayerStatus before, PlayerStatus after)
        {
            Debug.Log($"Player Status Changed: isReady {before.isReady}->{after.isReady}, isFinished {before.isFinished}->{after.isFinished}");

            // TODO :
            // 일단 클라이언트가 게임상태를 직접 변경하는 컨셉인데.. 
            // 플레이어 상태값을 서버에 주면, 서버가 알아서 상태를 변경하고 통지하는 Server-streaming 으로 바꿀필요가있음.

            // ready 됨
            if (!before.isReady && after.isReady)
            {
                Debug.Log("Player became ready");
                await UpdateStatusAsync(GameplayStatus.Ready);
            }
            // 끝남
            if (!before.isFinished && after.isFinished)
            {
                Debug.Log("Player finished - ending game");

                // [FIXED] 모든 상태 업데이트를 먼저 완료한 후 정리
                await UpdateStatusAsync(GameplayStatus.Ending);
                await UpdateStatusAsync(GameplayStatus.Terminated);

                // 모든 상태 업데이트 완료 후 할당 해제
                if (MultiplayMatchBlackboard.isMaster)
                {
                    await DeallocateAsync();
                }

                // 마지막에 씬 전환
                SceneManager.LoadScene("Lobbies");
            }
        }

        public async Task<(bool success, string message, string allocationId)> AllocateAsync()
        {
            // [ADDED] 할당 요청 전에 TCS 준비
            if (_readyTcs == null && !IsAllocationReady())
            {
                _readyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            AllocationPayload payload = new AllocationPayload
            {
                lobbyId = MultiplayMatchBlackboard.lobbyId,
                clientIds = new List<int>(MultiplayMatchBlackboard.clientIds),
                gameSettings = new Dictionary<string, string>()
                {
                    // TODO : setting 에 커스텀세팅 데이터 추가하고 이거 초기화할때 써야함.
                }
            };

            try
            {
                var payloadJson = JsonConvert.SerializeObject(payload);

                Debug.Log($"Creating allocation with payload: {payloadJson}");

                var response = await _multiGameplayClient.CreateAllocationAsync(new CreateAllocationRequest
                {
                    AllocationId = Guid.NewGuid().ToString(),
                    BuildConfigurationId = _multiplaySettings.buildConfigurationId,
                    RegionId = _multiplaySettings.regionId,
                    Restart = false,
                    Payload = payloadJson
                });

                Debug.Log($"CreateAllocation response received. ServerId: {response.Allocation?.ServerId}");

                // [CHANGED] ServerId 즉시 확인 대신 AllocationCreated 단계로 처리
                currentAllocation = response.Allocation;
                currentMatchId = response.Allocation.AllocationId;

                if (response.Allocation.ServerId > 0)
                {
                    Debug.Log("Allocation already ready!");
                    return (true, "Allocated game server.", currentAllocation.AllocationId);
                }
                else
                {
                    Debug.Log("Allocation created, waiting for ready...");
                    return (true, "Allocation requested. Waiting for ready...", currentMatchId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"AllocateAsync exception: {ex}");
                string message = $"Failed to allocate game server\n{ex.Message}.";

                // [ADDED] 실패 시 대기 중이면 예외 설정
                if (_isWaitingForReady)
                {
                    _readyTcs?.TrySetException(ex);
                }

                return (false, message, null);
            }
        }

        public async Task<bool> DeallocateAsync()
        {
            if (string.IsNullOrEmpty(currentMatchId))
            {
                Debug.Log("DeallocateAsync: No allocation to deallocate");
                return true;
            }

            string allocationIdToDelete = currentMatchId; // 미리 저장

            try
            {
                Debug.Log($"Deallocating server: {allocationIdToDelete}");
                await _multiGameplayClient.DeleteAllocationAsync(new DeleteAllocationRequest
                {
                    AllocationId = allocationIdToDelete,
                });

                Debug.Log($"Successfully deallocated server: {allocationIdToDelete}");
                ClearAllocationInfo(); // 성공 후에만 정리
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deallocate server {allocationIdToDelete}, {ex.Message}");
                return false;
            }
        }

        public async Task<(bool success, AllocationInfo allocatiion)> GetAllocation(string allocationId)
        {
            try
            {
                var response = await _multiGameplayClient.GetAllocationAsync(new GetAllocationRequest
                {
                    AllocationId = allocationId,
                });

                if (response.Allocation.ServerId > 0)
                {
                    currentAllocation = response.Allocation;
                    currentMatchId = response.Allocation.AllocationId;
                    return (true, currentAllocation);
                }
                else
                {
                    ClearAllocationInfo();
                    return (false, null);
                }
            }
            catch
            {
                ClearAllocationInfo();
                return (false, null);
            }
        }

        public async Task<(bool success, IList<AllocationInfo> allocations)> GetAllocations(int age, int limit, int offset, IEnumerable<string> allocationIds)
        {
            try
            {
                var response = await _multiGameplayClient.GetAllocationsAsync(new GetAllocationsRequest
                {
                    Age = $"{age}h",
                    Limit = limit,
                    Offset = offset,
                    AllocationIds = { allocationIds }
                });

                return (true, response.Allocations);
            }
            catch (Exception ex)
            {
                return (false, null);
            }
        }

        private void ClearAllocationInfo()
        {
            currentAllocation = null;
            currentMatchId = null;
        }

        public async Task<(bool success, GameplayStatus status)> UpdateStatusAsync(GameplayStatus newStatus)
        {
            try
            {
                // [ADDED] null 체크
                if (string.IsNullOrEmpty(currentMatchId))
                {
                    Debug.LogError("UpdateStatusAsync failed: currentMatchId is null or empty");
                    return (false, GameplayStatus.Unknown);
                }

                if (MultiplayMatchBlackboard.lobbyId <= 0)
                {
                    Debug.LogError("UpdateStatusAsync failed: lobbyId is invalid");
                    return (false, GameplayStatus.Unknown);
                }

                Debug.Log($"UpdateStatusAsync: AllocationId={currentMatchId}, LobbyId={MultiplayMatchBlackboard.lobbyId}, Status={newStatus}");

                await _multiGameplayClient.UpdateGameplayStatusAsync(new UpdateGameplayStatusRequest
                {
                    AllocationId = currentMatchId,
                    LobbyId = MultiplayMatchBlackboard.lobbyId,
                    Status = newStatus
                });

                currentStatus = newStatus;
                Debug.Log($"UpdateStatusAsync successful: {newStatus}");
                return (true, newStatus);
            }
            catch (Exception ex)
            {
                Debug.LogError($"UpdateStatusAsync failed: {ex.Message}");
                Debug.LogError($"currentMatchId: {currentMatchId ?? "NULL"}");
                Debug.LogError($"lobbyId: {MultiplayMatchBlackboard.lobbyId}");
                return (false, GameplayStatus.Unknown);
            }
        }

        public void SubscribeToAllocationEvents()
        {
            try
            {
                _cts = new CancellationTokenSource();

                _eventStream = _multiGameplayClient.SubscribeAllocationEvents(new SubscribeAllocationEventsRequest
                {
                    ClientId = GrpcConnection.clientInfo.ClientId,
                    LobbyId = MultiplayMatchBlackboard.lobbyId,
                }, cancellationToken: _cts.Token);

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var e in _eventStream.ResponseStream.ReadAllAsync(_cts.Token))
                        {
                            HandleAllocationEvent(e);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log("Error in allcationevent stream.");
                    }
                });

                Debug.Log($"Subscribed to allocation event for lobby {MultiplayMatchBlackboard.lobbyId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to subscribe to allocationi event. {ex}");
            }
        }

        /// <summary>
        /// 이 이벤트는 메인쓰레드에서 호출되지않음.
        /// 유니티 로직은 동기화Context 를 통해 Send/Post 하거나 Unity Awaitable 로 동기화 해주어야함.
        /// </summary>
        async void HandleAllocationEvent(AllocationEvent e)
        {
            await Awaitable.MainThreadAsync();
            Debug.Log($"=== AllocationEvent occurred: {e.Type} ===");

            switch (e.Type)
            {
                case AllocationEvent.Types.EventType.AllocationCreated:
                    {
                        Debug.Log($"AllocationCreated - AllocationId: {e.Allocation?.AllocationId}");
                        onAllocationCreated?.Invoke(e.Allocation);
                        MultiplayMatchBlackboard.allocation = e.Allocation;
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationReady:
                    {
                        Debug.Log($"AllocationReady - AllocationId: {e.Allocation?.AllocationId}, ServerId: {e.Allocation?.ServerId}");

                        // [ADDED] Ready 상태 업데이트 및 대기 해제
                        currentAllocation = e.Allocation;
                        currentMatchId = e.Allocation.AllocationId;
                        MultiplayMatchBlackboard.allocation = e.Allocation;

                        // [ADDED] 대기 중이면 완료 신호
                        if (_isWaitingForReady)
                        {
                            Debug.Log("Setting _readyTcs result to true");
                            _readyTcs?.TrySetResult(true);
                        }

                        OnAllocationReady();
                        onAllocationReady?.Invoke(e.Allocation);
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationDeleted:
                    {
                        Debug.Log($"AllocationDeleted - AllocationId: {e.Allocation?.AllocationId}");
                        onAllocatonDeleted?.Invoke();
                        MultiplayMatchBlackboard.allocation = e.Allocation;

                        // [ADDED] 대기 중이면 취소
                        if (_isWaitingForReady)
                        {
                            _readyTcs?.TrySetCanceled();
                        }
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationFailed:
                    {
                        Debug.LogError($"=== ALLOCATION FAILED ===");
                        Debug.LogError($"Error Message: {e.ErrorMessage}");
                        Debug.LogError($"Allocation Info: {JsonConvert.SerializeObject(e.Allocation, Formatting.Indented)}");

                        onAllocationFailed?.Invoke(e.ErrorMessage);
                        MultiplayMatchBlackboard.allocation = e.Allocation;

                        // [ADDED] 대기 중이면 예외 설정
                        if (_isWaitingForReady)
                        {
                            _readyTcs?.TrySetException(new Exception(e.ErrorMessage));
                        }
                    }
                    break;
                case AllocationEvent.Types.EventType.AllocationStatusChanged:
                    {
                        Debug.Log($"AllocationStatusChanged - New Status: {e.NewStatus}");
                        onAllocationGameplayStatusChanged?.Invoke(e.NewStatus);
                    }
                    break;
                default:
                    break;
            }
        }

        async void OnAllocationReady()
        {
            await Awaitable.MainThreadAsync();
            Debug.Log("OnAllocationReady - Creating player instance");
            _player = Instantiate(_playerPrefab);
            _player.onStatusChanged += OnPlayerStatusChanged;
        }
    }
}