using System.Collections.Concurrent;

namespace Persistence.Repositories
{
    public class InMemoryUserSessionRepository : IUserSessionRepository
    {
        public InMemoryUserSessionRepository()
        {
            _table = new ConcurrentDictionary<string, UserInfo>(); // TODO : Reserving (평균적인 동접자수)
        }


        ConcurrentDictionary<string, UserInfo> _table;


        public void Create(string sessionId, UserInfo userInfo)
        {
            _table[sessionId] = userInfo;
        }

        public UserInfo Get(string sessionId)
        {
            if (_table.TryGetValue(sessionId, out UserInfo userInfo))
                return userInfo;

            return null;
        }

        public void Remove(string sessionId)
        {
            if (_table.TryRemove(sessionId, out UserInfo userInfo))
                return;

            throw new Exception($"Failed to remove userInfo. Session {sessionId} does not exist.");
        }

        public void Update(string sessionId, UserInfo userInfo)
        {
            _table.AddOrUpdate(sessionId, userInfo, (key, oldValue) => userInfo);
        }
    }
}
