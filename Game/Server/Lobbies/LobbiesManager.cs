using Game.Lobbies;
using Google.Protobuf.Collections;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Server.Network;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Server.Lobbies
{
    class LobbiesManager
    {
        public LobbiesManager(int maxLobbies = 100) 
        {
            _idGenerator = new IdGenerator(maxLobbies);
        }


        public IEnumerable<Lobby> All => _lobbies.Values;


        public class Lobby
        {
            public int Id { get; init; }
            public int HostClientId { get; init; }
            public int MaxClient { get; init; }
            public HashSet<int> Members { get; } = new();
            public Dictionary<string, string> CustomProperties { get; } = new();
            public Dictionary<int, Dictionary<string, string>> MemberCustomProperties { get; } = new();
            public ConcurrentList<IServerStreamWriter<LobbyEvent>> EventStreams { get; } = new();
        }

        private readonly ConcurrentDictionary<int, Lobby> _lobbies = new();
        private IdGenerator _idGenerator;
        private readonly object _lobbyGate = new object();

        public bool TryGetLobby(int lobbyId, out Lobby lobby) => _lobbies.TryGetValue(lobbyId, out lobby);

        public int Create(int hostClientId, int maxClient)
        {
            int lobbyId = _idGenerator.AssignId(); // IdGenerator 가 Thread-safe 한걸로 바꿔야함.

            if (lobbyId < 0)
                return lobbyId;

            Lobby lobby = new Lobby
            {
                Id = lobbyId,
                HostClientId = hostClientId,
                MaxClient = maxClient,
            };

            lobby.Members.Add(hostClientId);
            lobby.MemberCustomProperties[hostClientId] = new();
            _lobbies[lobbyId] = lobby;
            return lobbyId;
        }

        public bool Join(int lobbyId, int clientId)
        {
            // 참여할 로비없음
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return false;

            lock (_lobbyGate)
            {
                // 풀방임
                if (lobby.Members.Count >= lobby.MaxClient)
                    return false;

                lobby.Members.Add(clientId);
                lobby.MemberCustomProperties[clientId] = new();
                return true;
            }
        }

        public bool Leave(int lobbyId, int clientId)
        {
            // 떠나려는 로비가 이미 사라지고없다..
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return false;

            lock (_lobbyGate)
            {
                // 이 클라이언트는 이 로비 소속이 아니다
                if (!lobby.Members.Remove(clientId))
                    return false;

                lobby.MemberCustomProperties.Remove(clientId);
            }

            // 방금 떠난 멤버가 마지막 멤버였다면 로비 폐기
            if (lobby.Members.Count == 0)
            {
                _lobbies.Remove(lobbyId, out var _);
                _idGenerator.ReleaseId(lobbyId);
            }

            return true;
        }

        public void AddLobbyEventStream(int lobbyId, IServerStreamWriter<LobbyEvent> serverStreamWriter)
        {
            // 구독할 로비 없음
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return;

            lobby.EventStreams.Add(serverStreamWriter);
        }

        public void RemoveLobbyEventStream(int lobbyId, IServerStreamWriter<LobbyEvent> serverStreamWriter)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return;

            lobby.EventStreams.Remove(serverStreamWriter);
        }

        /// <summary>
        /// ServerStreaming 진행하고 
        /// 데이터를 전송하지못하면 연결 끊어진것으로 간주해서 제거
        /// </summary>
        public async Task Broadcast(int lobbyId, LobbyEvent e)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return;

            var copy = lobby.EventStreams.ToList();

            var tasks = copy.Select(async stream =>
            {
                try
                {
                    await stream.WriteAsync(e);
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
                lobby.EventStreams.Remove(stream);
            }
        }

        public bool SetLobbyCustomProperties(int lobbyId, IDictionary<string, string> kv)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return false;

            lock (_lobbyGate)
            {
                foreach (var (k, v) in kv)
                {
                    lobby.CustomProperties[k] = v;
                }
            }

            return true;
        }

        public bool SetUserCustomProperties(int lobbyId, int clientId, IDictionary<string, string> kv)
        {
            if (!_lobbies.TryGetValue(lobbyId, out var lobby))
                return false;

            lock (_lobbyGate)
            {
                if (!lobby.Members.Contains(clientId))
                    return false;

                foreach (var (k, v) in kv)
                {
                    lobby.MemberCustomProperties[clientId][k] = v;
                }
            }

            return true;
        }

        public LobbyInfo ToLobbyInfo(Lobby lobby)
        {
            lock (_lobbyGate)
                return new LobbyInfo
                {
                    LobbyId = lobby.Id,
                    HostClientId = lobby.HostClientId,
                    MaxClient = lobby.MaxClient,
                    NumClient = lobby.Members.Count,
                    CustomProperties = { lobby.CustomProperties },
                };
        }

        public LobbyInfo ToLobbyInfo(int lobbyId)
        {
            Lobby lobby = _lobbies[lobbyId];

            lock (_lobbyGate)
                return new LobbyInfo
                {
                    LobbyId = lobby.Id,
                    HostClientId = lobby.HostClientId,
                    MaxClient = lobby.MaxClient,
                    NumClient = lobby.Members.Count,
                    CustomProperties = { lobby.CustomProperties },
                };
        }

        public UserInLobbyInfo ToUserInLobbyInfo(Lobby lobby, int clientId)
        {
            lock (_lobbyGate)
                return new UserInLobbyInfo
                {
                    ClientId = clientId,
                    CustomProperties = { lobby.MemberCustomProperties[clientId] }
                };
        }

        public UserInLobbyInfo ToUserInLobbyInfo(int lobbyId, int clientId)
        {
            Lobby lobby = _lobbies[lobbyId];

            lock (_lobbyGate)
                return new UserInLobbyInfo
                {
                    ClientId = clientId,
                    CustomProperties = { lobby.MemberCustomProperties[clientId] }
                };
        }
    }

    public class ConcurrentList<T> : IEnumerable<T>
    {
        private List<T> _list = new();
        private readonly ReaderWriterLockSlim _rwSlim = new(); // 단일쓰기 및 다중읽기

        public void Add(T item)
        {
            _rwSlim.EnterWriteLock();
            _list.Add(item);
            _rwSlim.ExitWriteLock();
        }

        public bool Remove(T item)
        {
            bool result = false;
            _rwSlim.EnterWriteLock();
            result = _list.Remove(item);
            _rwSlim.ExitWriteLock();
            return result;
        }

        public List<T> ToList()
        {
            List<T> list;
            _rwSlim.EnterReadLock();
            list = new List<T>(_list);
            _rwSlim.ExitReadLock();
            return list;

            //_rwSlim.EnterReadLock();
            //try { return new List<T>(_list); }
            //finally { _rwSlim.ExitReadLock(); }
        }

        public IEnumerator<T> GetEnumerator()
        {
            IEnumerator<T> e = null;
            _rwSlim.EnterReadLock();
            e = _list.GetEnumerator();
            _rwSlim.ExitReadLock();
            return e;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
