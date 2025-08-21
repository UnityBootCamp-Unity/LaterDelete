using Game.Multigameplay.V1;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Game.Server.MultiGameplay
{
    class MultiGameServerManager
    {
        public MultiGameServerManager(ILogger<MultiGameServerManager> logger)
        {
            _logger = logger;
            _matches = new ConcurrentDictionary<string, MatchInfo>();
            _serverStatuses = new ConcurrentDictionary<int, ServerStatusInfo>();
            _allocationEventStreams = new ConcurrentDictionary<int, ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>>();

            // 신규(재접속)
            _sessions = new ConcurrentDictionary<string, SessionInfo>(); // key = $"{lobbyId}:{clientId}"
            _lobbyLastActivityUtc = new ConcurrentDictionary<int, DateTime>();
            _deallocTimers = new ConcurrentDictionary<int, CancellationTokenSource>();
        }

        private readonly ConcurrentDictionary<string, MatchInfo> _matches; // k: MatchId, v: MatchInfo
        private readonly ConcurrentDictionary<int, ServerStatusInfo> _serverStatuses; // k: LobbyId, v: ServerStatusInfo
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>> _allocationEventStreams; // k: lobbyId, v: (clientId, stream)
        private ILogger<MultiGameServerManager> _logger;

        // ===== 신규 필드 =====
        private readonly ConcurrentDictionary<string, SessionInfo> _sessions;
        private readonly ConcurrentDictionary<int, DateTime> _lobbyLastActivityUtc;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _deallocTimers;

        // 재접속 대기(5분)
        private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan EmptyDeallocDelay = TimeSpan.FromMinutes(5);

        private static string Key(int lobbyId, int clientId) => $"{lobbyId}:{clientId}";

        public Task RegisterMatchAsync(MatchInfo matchInfo)
        {
            _matches[matchInfo.MatchId] = matchInfo;
            _serverStatuses[matchInfo.LobbyId] = new ServerStatusInfo
            {
                Status = GameplayStatus.Unknown,
            };
            _logger.LogInformation($"Registered match {matchInfo.MatchId}");
            return Task.CompletedTask;
        }

        public Task UnregisterMatchAsync(string matchId)
        {
            if (_matches.TryRemove(matchId, out var matchInfo))
            {
                _logger.LogInformation($"Unregistered match {matchId}");
            }

            return Task.CompletedTask;
        }

        public Task<MatchInfo> GetMatchAsync(string matchId)
        {
            _matches.TryGetValue(matchId, out var matchInfo);
            return Task.FromResult(matchInfo);
        }

        public Task<MatchInfo> GetMatchByLobbyIdAsync(int lobbyId)
        {
            var match = _matches.Values.FirstOrDefault(m => m.LobbyId == lobbyId);
            return Task.FromResult(match);
        }

        public int GetSubscriberCount(int lobbyId)
        {
            return _allocationEventStreams[lobbyId].Count;
        }

        public Task UpdateServerStatusAsync(int lobbyId, ServerStatusInfo serverStatus)
        {
            _serverStatuses[lobbyId] = serverStatus;
            return Task.CompletedTask;
        }

        public ServerStatusInfo GetServerStatus(int lobbyId)
        {
            return _serverStatuses[lobbyId];
        }

        public void AddAllocationEventStream(int lobbyId, int clientId, IServerStreamWriter<AllocationEvent> stream)
        {
            var lobbyStreams = _allocationEventStreams.GetOrAdd(lobbyId, _ => new ConcurrentDictionary<int, IServerStreamWriter<AllocationEvent>>());
            lobbyStreams[clientId] = stream;
            _logger.LogInformation($"Added allocation event stream for client {clientId} in lobby {lobbyId}");
        }

        public void RemoveAllocationEventStream(int lobbyId, int clientId)
        {
            if (_allocationEventStreams.TryGetValue(lobbyId, out var lobbyStreams))
            {
                lobbyStreams.TryRemove(clientId, out _);

                // 방금 접속종료된 클라이언트가 현재로비의 마지막 클라이언트였다면 로비의 ConcurrentDictonary 제거
                if (lobbyStreams.IsEmpty)
                {
                    _allocationEventStreams.TryRemove(lobbyId, out _);
                }
            }

            _logger.LogInformation($"Removed allocation event stream for client {clientId} in lobby {lobbyId}");
        }

        public async Task BroadcastAllocationEventAsync(int lobbyId, AllocationEvent e)
        {
            if (!_allocationEventStreams.TryGetValue(lobbyId, out var lobbyStreams))
                return;

            var copy = lobbyStreams.ToList();

            var tasks = copy.Select(async stream =>
            {
                try
                {
                    await stream.Value.WriteAsync(e);
                    return (stream, success: true);
                }
                catch
                {
                    return (stream, success: false);
                }
            });

            var results = await Task.WhenAll(tasks);

            var failedStreams = results
                .Where(r => !r.success)
                .Select(r => r.stream);

            foreach (var stream in failedStreams)
            {
                lobbyStreams.TryRemove(stream.Key, out _);
            }
        }

        // --- 신규: 로비 활동 갱신 ---
        public void TouchLobbyActivity(int lobbyId)
        {
            _lobbyLastActivityUtc[lobbyId] = DateTime.UtcNow;
            CancelDeallocTimer(lobbyId);
        }

        // --- 신규: 세션 발급/복구 ---
        public (bool ok, bool resumed, string sessionToken, MatchInfo match, ServerStatusInfo status, string reason)
            ResumeOrIssueSession(int lobbyId, int clientId, string providedToken)
        {
            var key = Key(lobbyId, clientId);
            var now = DateTime.UtcNow;
            _lobbyLastActivityUtc.AddOrUpdate(lobbyId, now, (_, __) => now);

            if (_sessions.TryGetValue(key, out var sess))
            {
                // 기존 세션 존재
                if (!string.IsNullOrEmpty(providedToken) && providedToken == sess.SessionToken)
                {
                    // 토큰 일치 → 복구 가능 여부 판단
                    if (sess.State == SessionState.Grace && now <= sess.ReconnectDeadlineUtc)
                    {
                        sess.State = SessionState.Active;
                        sess.LastSeenUtc = now;
                        _sessions[key] = sess;

                        _logger.LogInformation($"Resumed session (lobby {lobbyId}, client {clientId}).");
                        CancelDeallocTimer(lobbyId);
                        return (true, true, sess.SessionToken, GetMatchByLobbyIdAsync(lobbyId).Result, GetServerStatusSafe(lobbyId), "");
                    }
                    if (sess.State == SessionState.Active)
                    {
                        // 이미 Active: 토큰 재인증 통과로 본다
                        sess.LastSeenUtc = now;
                        _sessions[key] = sess;
                        CancelDeallocTimer(lobbyId);
                        return (true, true, sess.SessionToken, GetMatchByLobbyIdAsync(lobbyId).Result, GetServerStatusSafe(lobbyId), "");
                    }

                    // Expired 또는 Grace 만료
                    return (false, false, "", null, null, "Session expired.");
                }
                else
                {
                    // 토큰 불일치 → 새 발급(동일 clientId 재합류 케이스)
                    var newToken = Guid.NewGuid().ToString("N");
                    sess.SessionToken = newToken;
                    sess.State = SessionState.Active;
                    sess.LastSeenUtc = now;
                    sess.ReconnectDeadlineUtc = DateTime.MinValue;
                    _sessions[key] = sess;

                    _logger.LogInformation($"Reissued session token (lobby {lobbyId}, client {clientId}).");
                    CancelDeallocTimer(lobbyId);
                    return (true, false, newToken, GetMatchByLobbyIdAsync(lobbyId).Result, GetServerStatusSafe(lobbyId), "");
                }
            }
            else
            {
                // 신규 접속 → 세션 발급
                var token = Guid.NewGuid().ToString("N");
                var newSess = new SessionInfo
                {
                    LobbyId = lobbyId,
                    ClientId = clientId,
                    SessionToken = token,
                    State = SessionState.Active,
                    LastSeenUtc = now,
                    ReconnectDeadlineUtc = DateTime.MinValue
                };
                _sessions[key] = newSess;

                _logger.LogInformation($"Issued new session (lobby {lobbyId}, client {clientId}).");
                CancelDeallocTimer(lobbyId);
                return (true, false, token, GetMatchByLobbyIdAsync(lobbyId).Result, GetServerStatusSafe(lobbyId), "");
            }
        }

        // --- 신규: 스트림 추가/제거 훅에서 세션 상태 갱신 ---
        public void OnStreamAdded(int lobbyId, int clientId)
        {
            var key = Key(lobbyId, clientId);
            var now = DateTime.UtcNow;

            _sessions.AddOrUpdate(key,
                addValueFactory: _ => new SessionInfo
                {
                    LobbyId = lobbyId,
                    ClientId = clientId,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    State = SessionState.Active,
                    LastSeenUtc = now
                },
                updateValueFactory: (_, s) =>
                {
                    s.State = SessionState.Active;
                    s.LastSeenUtc = now;
                    return s;
                });

            TouchLobbyActivity(lobbyId);
        }

        public void OnStreamRemoved(int lobbyId, int clientId)
        {
            var key = Key(lobbyId, clientId);
            var now = DateTime.UtcNow;

            if (_sessions.TryGetValue(key, out var s))
            {
                s.State = SessionState.Grace;
                s.ReconnectDeadlineUtc = now + GracePeriod;
                s.LastSeenUtc = now;
                _sessions[key] = s;
            }
            else
            {
                // 세션이 처음부터 없던 경우도 그레이스로 기록하여 일관성 보장
                _sessions[key] = new SessionInfo
                {
                    LobbyId = lobbyId,
                    ClientId = clientId,
                    SessionToken = Guid.NewGuid().ToString("N"),
                    State = SessionState.Grace,
                    LastSeenUtc = now,
                    ReconnectDeadlineUtc = now + GracePeriod
                };
            }

            _lobbyLastActivityUtc[lobbyId] = now;
            TryArmDeallocTimer(lobbyId);
        }

        // --- 신규: 로비의 활동/인원 스냅샷 ---
        private (int active, int grace, DateTime? maxGraceDeadlineUtc) GetLobbySessionSnapshot(int lobbyId)
        {
            int active = 0;
            int grace = 0;
            DateTime? maxDeadline = null;
            var now = DateTime.UtcNow;

            foreach (var kv in _sessions)
            {
                // key prefix: "lobbyId:"
                if (!kv.Key.StartsWith(lobbyId.ToString() + ":")) continue;
                var s = kv.Value;

                if (s.State == SessionState.Active)
                {
                    active++;
                }
                else if (s.State == SessionState.Grace)
                {
                    if (now <= s.ReconnectDeadlineUtc)
                    {
                        grace++;
                        if (maxDeadline == null || s.ReconnectDeadlineUtc > maxDeadline)
                            maxDeadline = s.ReconnectDeadlineUtc;
                    }
                    else
                    {
                        // Grace 만료 → Expired로 태깅
                        s.State = SessionState.Expired;
                        _sessions[kv.Key] = s;
                    }
                }
            }

            return (active, grace, maxDeadline);
        }

        private ServerStatusInfo GetServerStatusSafe(int lobbyId)
        {
            if (_serverStatuses.TryGetValue(lobbyId, out var s)) return s;
            return new ServerStatusInfo { Status = GameplayStatus.Unknown, TotalPlayers = 0, MaxPlayers = 0 };
        }

        // --- 신규: Dealloc 타이머 관리 ---
        private void CancelDeallocTimer(int lobbyId)
        {
            if (_deallocTimers.TryRemove(lobbyId, out var cts))
            {
                try { cts.Cancel(); cts.Dispose(); } catch { /* ignore */ }
                _logger.LogInformation($"Dealloc timer cancelled for lobby {lobbyId}");
            }
        }

        public void TryArmDeallocTimer(int lobbyId)
        {
            var (active, grace, maxGraceDeadline) = GetLobbySessionSnapshot(lobbyId);
            var now = DateTime.UtcNow;

            // Active가 있으면 절대 타이머 안 건다.
            if (active > 0)
            {
                CancelDeallocTimer(lobbyId);
                return;
            }

            // 이미 타이머가 있으면 재설정(취소 후 다시)
            CancelDeallocTimer(lobbyId);

            var cts = new CancellationTokenSource();
            _deallocTimers[lobbyId] = cts;

            // 예약 시각 계산
            DateTime fireAt;
            if (grace > 0 && maxGraceDeadline.HasValue)
            {
                fireAt = maxGraceDeadline.Value; // 마지막 Grace 만료 시간
            }
            else
            {
                // 완전 무인 → 마지막 활동 이후 5분
                var last = _lobbyLastActivityUtc.TryGetValue(lobbyId, out var t) ? t : now;
                fireAt = last + EmptyDeallocDelay;
            }

            var delay = fireAt > now ? fireAt - now : TimeSpan.Zero;
            _ = Task.Run(async () =>
            {
                try
                {
                    _logger.LogInformation($"Dealloc timer armed for lobby {lobbyId}, due in {delay.TotalSeconds:F0}s");
                    await Task.Delay(delay, cts.Token);

                    // 만료 시점 재검증
                    var (a2, g2, _) = GetLobbySessionSnapshot(lobbyId);
                    if (a2 == 0 && g2 == 0)
                    {
                        await DeallocateLobbyAsync(lobbyId);
                    }
                    else
                    {
                        _logger.LogInformation($"Dealloc aborted for lobby {lobbyId} (active:{a2}, grace:{g2})");
                    }
                }
                catch (TaskCanceledException) { /* normal */ }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Dealloc timer task error for lobby {lobbyId}");
                }
                finally
                {
                    CancelDeallocTimer(lobbyId); // tidy
                }
            });
        }

        private async Task DeallocateLobbyAsync(int lobbyId)
        {
            try
            {
                var match = await GetMatchByLobbyIdAsync(lobbyId);
                if (match == null || string.IsNullOrEmpty(match.MatchId))
                {
                    _logger.LogWarning($"DeallocateLobbyAsync: no match for lobby {lobbyId}");
                    return;
                }

                _logger.LogInformation($"Auto-deallocating allocation {match.MatchId} (lobby {lobbyId}) due to inactivity.");
                await UnityMultiplayerGameServerHostingFacade.DeleteAllocationAsync(match.MatchId);

                // 브로드캐스트(알림)
                await BroadcastAllocationEventAsync(lobbyId, new AllocationEvent
                {
                    Type = AllocationEvent.Types.EventType.AllocationDeleted,
                    AllocationId = match.MatchId,
                    LobbyId = lobbyId,
                    Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
                });

                // 로컬 정리
                await UnregisterMatchAsync(match.MatchId);

                // 세션 만료 태깅
                foreach (var kv in _sessions.Where(kv => kv.Key.StartsWith(lobbyId.ToString() + ":")).ToList())
                {
                    var s = kv.Value; s.State = SessionState.Expired; _sessions[kv.Key] = s;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Auto-deallocation failed for lobby {lobbyId}");
            }
        }



        public class MatchInfo
        {
            public string MatchId { get; set; }
            public long ServerId { get; set; }
            public string ServerIp { get; set; }
            public ulong ServerPort { get; set; }
            public int LobbyId { get; set; }
            public List<int> ClientIds { get; set; }
        }

        public class ServerStatusInfo
        {
            public GameplayStatus Status { get; set; }
            public int TotalPlayers { get; set; } // 현재 총 플레이어수
            public int MaxPlayers { get; set; }
        }

        // --- 신규: 세션 정보 구조 ---
        public class SessionInfo
        {
            public int LobbyId { get; set; }
            public int ClientId { get; set; }
            public string SessionToken { get; set; }
            public SessionState State { get; set; } = SessionState.Active;
            public DateTime LastSeenUtc { get; set; }
            public DateTime ReconnectDeadlineUtc { get; set; }
        }

        public enum SessionState { Active = 0, Grace = 1, Expired = 2 }
    }
}