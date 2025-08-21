using Game.Chat;
using Game.Server.Auth;
using Game.Server.Network;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System.Collections.Concurrent;

namespace Game.Server.Chat
{
    class ChatServiceImpl : ChatService.ChatServiceBase
    {
        // 클라이언트별 채팅메세지 스트림에 쓰기위한 Writer 저장소
        private static readonly ConcurrentDictionary<int, IServerStreamWriter<ChatMessage>> _clients = new();

        /// <summary>
        /// 양방향 스트리밍 채팅
        /// 클라이언트에게 메세지 받아서 모든 클라이언트에게 브로드캐스트
        /// </summary>
        /// <param name="requestStream"> 클라이언트에게 받는 메세지 스트림 (읽기) </param>
        /// <param name="responseStream"> 클라이언트에게 주는 메세지 스트림 (쓰기) </param>
        /// <param name="context"> gRPC 호출 흐름 </param>
        /// <returns></returns>
        public override async Task Chat(IAsyncStreamReader<ChatMessage> requestStream, IServerStreamWriter<ChatMessage> responseStream, ServerCallContext context)
        {
            var jwtHeader = context.RequestHeaders.GetValue("authorization") ?? string.Empty;
            var jwt = jwtHeader.Replace("Bearer ", string.Empty);

            var (isValid, sessionId) = await AuthFacade.ValidateAsync(PersistenceApiSettings.BASE_URL, jwt, context.CancellationToken);

            if (!isValid)
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid jwt"));

            // 클라이언트에게 받은 모든 메세지를 비동기로 처리 (소비)
            await foreach (ChatMessage message in requestStream.ReadAllAsync())
            {
                message.Timestamp = Timestamp.FromDateTime(DateTime.UtcNow);
                _clients[message.SenderId] = responseStream; // echo. 메세지보낸 클라이언트한테도 메세지 돌려줌 (브로드캐스트 대상에 포함)
                await BroadcastAsync(message);
            }

            _clients.TryRemove(context.GetHashCode(), out _); // 클라이언트 연결 종료시 활성 클라이언트 목록에서 제거
        }

        /// <summary>
        /// 모든 연결된 클라이언트에게 메세지 브로드캐스트
        /// </summary>
        /// <param name="message"> 브로드캐스트 할 메세지 </param>
        private static async Task BroadcastAsync(ChatMessage message)
        {
            foreach (var client in _clients)
            {
                try
                {
                    await client.Value.WriteAsync(message);
                }
                catch
                {
                    // Nothing to do
                }
            }
        }
    }
}
