namespace Server.Network
{
    /// <summary>
    /// 고유 Id 를 부여하기위한 생성기
    /// TODO : Thread safe 하게 바꿔야함
    /// </summary>
    class IdGenerator
    {
        public IdGenerator(int maxClients = 100)
        {
             _idSet = new HashSet<int>(maxClients);
            _availableIdQueue = new Queue<int>(maxClients);

            // 0 번을 id 로 쓰면  default 값 처리하기 불편하다.
            for (int i = 1; i <= maxClients; i++)
            {
                _availableIdQueue.Enqueue(i);
            }
        }

        readonly HashSet<int> _idSet; // 현재 연결된 세션들의 id 집합
        readonly Queue<int> _availableIdQueue; // id 를 재사용하기위한 큐

        /// <summary>
        /// ID 를 부여하기위함
        /// </summary>
        /// <returns> 할당되는 id </returns>
        public int AssignId()
        {
            if (_availableIdQueue.Count > 0)
            {
                int id = _availableIdQueue.Dequeue();
                _idSet.Add(id);
                return id;
            }
            else
            {
                Console.WriteLine($"[{nameof(IdGenerator)}] : Server is fulled... reached to max clients");
                return -1;
            }
        }

        /// <summary>
        /// ID 를 반납하기 위함
        /// </summary>
        /// <param name="id"> 반납할 id </param>
        public void ReleaseId(int id)
        {
            if (_idSet.Remove(id))
            {
                _availableIdQueue.Enqueue(id);
            }
            else
            {
                // TODO : 예외
            }
        }
    }
}
