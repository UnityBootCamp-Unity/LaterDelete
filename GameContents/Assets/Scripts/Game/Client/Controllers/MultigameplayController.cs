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

        // [ADDED] Ready ��� �ý���
        private TaskCompletionSource<bool> _readyTcs;
        private bool _isWaitingForReady = false;

        public event Action<AllocationInfo> onAllocationCreated;
        public event Action<AllocationInfo> onAllocationReady;
        public event Action onAllocatonDeleted;
        public event Action<string> onAllocationFailed;
        public event Action<GameplayStatus> onAllocationGameplayStatusChanged;

        // -- �ű� --
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

            // Resume -> ���� ���� ���� -> (�����͸�) Allocate �� ���� ����
            await ResumeOrIssueSessionAsync();

            SubscribeToAllocationEvents();

            Debug.Log($"isMaster: {MultiplayMatchBlackboard.isMaster}");

            // TODO : ��� Client �� ���� �Ϸ�ɶ����� ��ٸ�
            await Task.Delay(500); // �̺�Ʈ ���� �����ð�

            if (MultiplayMatchBlackboard.isMaster)
            {
                Debug.Log("=== Starting Allocation Process (Master) ===");

                var allocateResponse = await AllocateAsync(); // Ŭ���̾�Ʈ�� ���� Allocation ��û�ϴ·������ٴ� ������ ���� Ȯ���ϸ鼭 �˾Ƽ� ó���ϴ°� ���Ȼ� ����

                Debug.Log($"AllocateAsync result: success={allocateResponse.success}, message={allocateResponse.message}");

                if (!allocateResponse.success)
                {
                    Debug.LogError($"Allocation failed: {allocateResponse.message}");
                    throw new Exception(allocateResponse.message); // TODO : ��õ� �� ����ó��, �κ񺹱� �� �ؾ���
                }

                // [ADDED] Ready �̺�Ʈ ��� - AllocationAsync�� ���������� ���� Ready�� �ƴ� �� ����
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

        // [ADDED] ���� �Ҵ��� Ready �������� Ȯ��
        private bool IsAllocationReady()
        {
            return currentAllocation != null && currentAllocation.ServerId > 0;
        }

        // [ADDED] AllocationReady �̺�Ʈ�� ��ٸ��� �޼���
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
                    // �ű� �߱޵� ������ ���. �ʿ� �� UI ǥ��.
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
                        // ���� ����
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
                        // ���� ���
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Allocation event stream error: {ex.Message}");
                    }

                    if (_eventLoopCts.IsCancellationRequested) break;

                    // ����ٸ� 1) Resume ��õ� -> 2) ��� ��� �� �籸��
                    await ResumeOrIssueSessionAsync();
                    await Task.Delay(2000);
                }
            }, _eventLoopCts.Token);
        }

        private async void OnPlayerStatusChanged(PlayerStatus before, PlayerStatus after)
        {
            Debug.Log($"Player Status Changed: isReady {before.isReady}->{after.isReady}, isFinished {before.isFinished}->{after.isFinished}");

            // TODO :
            // �ϴ� Ŭ���̾�Ʈ�� ���ӻ��¸� ���� �����ϴ� �����ε�.. 
            // �÷��̾� ���°��� ������ �ָ�, ������ �˾Ƽ� ���¸� �����ϰ� �����ϴ� Server-streaming ���� �ٲ��ʿ䰡����.

            // ready ��
            if (!before.isReady && after.isReady)
            {
                Debug.Log("Player became ready");
                await UpdateStatusAsync(GameplayStatus.Ready);
            }
            // ����
            if (!before.isFinished && after.isFinished)
            {
                Debug.Log("Player finished - ending game");

                // [FIXED] ��� ���� ������Ʈ�� ���� �Ϸ��� �� ����
                await UpdateStatusAsync(GameplayStatus.Ending);
                await UpdateStatusAsync(GameplayStatus.Terminated);

                // ��� ���� ������Ʈ �Ϸ� �� �Ҵ� ����
                if (MultiplayMatchBlackboard.isMaster)
                {
                    await DeallocateAsync();
                }

                // �������� �� ��ȯ
                SceneManager.LoadScene("Lobbies");
            }
        }

        public async Task<(bool success, string message, string allocationId)> AllocateAsync()
        {
            // [ADDED] �Ҵ� ��û ���� TCS �غ�
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
                    // TODO : setting �� Ŀ���Ҽ��� ������ �߰��ϰ� �̰� �ʱ�ȭ�Ҷ� �����.
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

                // [CHANGED] ServerId ��� Ȯ�� ��� AllocationCreated �ܰ�� ó��
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

                // [ADDED] ���� �� ��� ���̸� ���� ����
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

            string allocationIdToDelete = currentMatchId; // �̸� ����

            try
            {
                Debug.Log($"Deallocating server: {allocationIdToDelete}");
                await _multiGameplayClient.DeleteAllocationAsync(new DeleteAllocationRequest
                {
                    AllocationId = allocationIdToDelete,
                });

                Debug.Log($"Successfully deallocated server: {allocationIdToDelete}");
                ClearAllocationInfo(); // ���� �Ŀ��� ����
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
                // [ADDED] null üũ
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
        /// �� �̺�Ʈ�� ���ξ����忡�� ȣ���������.
        /// ����Ƽ ������ ����ȭContext �� ���� Send/Post �ϰų� Unity Awaitable �� ����ȭ ���־����.
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

                        // [ADDED] Ready ���� ������Ʈ �� ��� ����
                        currentAllocation = e.Allocation;
                        currentMatchId = e.Allocation.AllocationId;
                        MultiplayMatchBlackboard.allocation = e.Allocation;

                        // [ADDED] ��� ���̸� �Ϸ� ��ȣ
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

                        // [ADDED] ��� ���̸� ���
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

                        // [ADDED] ��� ���̸� ���� ����
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