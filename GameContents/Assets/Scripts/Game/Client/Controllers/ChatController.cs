using Game.Chat;
using Game.Client.Network;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Client.Controllers
{
    public class ChatController : MonoBehaviour
    {
        private ChatService.ChatServiceClient _chatClient;
        private CancellationTokenSource _cts;
        private AsyncDuplexStreamingCall<ChatMessage, ChatMessage> _stream; // 양방향 gRPC 인터셉터

        public event Action<(int senderId, int receiverId, DateTime time, string content)> onMessageReceived;

        private async void Start()
        {
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            _cts = new CancellationTokenSource();
            _chatClient = new ChatService.ChatServiceClient(GrpcConnection.channel);
            _stream = _chatClient.Chat(headers: GrpcConnection.JwtHeader(),
                                       cancellationToken: _cts.Token);
            _ = ReceiveLoopAsync();
        }

        public async Task SendAsync(string text)
        {
            await _stream.RequestStream.WriteAsync(new ChatMessage
            {
                SenderId = GrpcConnection.clientInfo.ClientId,
                ReceiverId = -1, // Broadcast
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
                Content = text,
            });
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                await foreach (var message in _stream.ResponseStream.ReadAllAsync(_cts.Token))
                {
                    onMessageReceived?.Invoke((message.SenderId, message.ReceiverId, message.Timestamp.ToDateTime(), message.Content));
                }
            }
            catch
            {
                // Nothing to do
            }
        }
    }
}