using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Game.Core.Network
{
    /// <summary>
    /// Socket 의 생명주기 및 Socket 을 통한 데이터 송수신 처리
    /// </summary>
    public abstract class TcpSession : IDisposable
    {
        public TcpSession(Socket socket, int bufferSize = 16 * KB)
        {
            Socket = socket;
            Socket.NoDelay = true;
            _receiveBuffer = new byte[bufferSize];
            _sendQueue = new ConcurrentQueue<ArraySegment<byte>>();
        }

        const int KB = 1_024;
        const int HEADER_SIZE = sizeof(int);
        const int MAX_PACKET_SIZE = 1 * KB * KB;

        public bool IsConnected => Socket.Connected;

        protected Socket Socket;

        // Send
        private readonly ConcurrentQueue<ArraySegment<byte>> _sendQueue; // Segment 전송 대기열
        private readonly object _sendGate = new object();
        private bool _isSending;
        private SocketAsyncEventArgs _sendArgs;

        // Receive
        private readonly byte[] _receiveBuffer;
        private int _receiveBufferCount;

        // Dispose
        private bool _disposed;

        // Connection
        public event Action OnConnected;
        public event Action OnDisconnected;


        public virtual void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TcpSession));

            OnConnected?.Invoke();
            _ = ReceiveLoopAsync();
            _sendArgs = new SocketAsyncEventArgs();
            _sendArgs.Completed += OnSendCompleted;
        }


        private void OnSendCompleted(object sender, SocketAsyncEventArgs args)
        {
            var token = (TaskCompletionSource<int>)args.UserToken;

            if (args.SocketError == SocketError.Success)
            {
                token.SetResult(args.BytesTransferred); // 송신 성공시 송신한 바이트수 결과
            }
            else
            {
                token.SetResult(0); // 송신 실패하면 바이트수 0
            }
        }

        protected virtual void Send(byte[] body)
        {
            if (body == null) 
                throw new ArgumentNullException("body");

            if (body.Length > MAX_PACKET_SIZE) 
                throw new ArgumentException("Packet too large");

            if (!IsConnected) 
                return;

            // 데이터 송신시, body 길이를 포함한 헤더를 붙여서 보낼거임.
            // Body 에 Header 붙인 길이
            int total = HEADER_SIZE + body.Length;
            byte[] frame = new byte[total];
            Buffer.BlockCopy(BitConverter.GetBytes(body.Length), 0, frame, 0, HEADER_SIZE); // 헤더
            Buffer.BlockCopy(body, 0, frame, HEADER_SIZE, body.Length);

            _sendQueue.Enqueue(new ArraySegment<byte>(frame, 0, frame.Length));
            Send();
        }

        private void Send()
        {
            lock (_sendGate)
            {
                // 이미 보내고있거나 소켓 해제되었으면 종료
                if (_isSending || _disposed)
                    return;

                // 송신할 데이터 없으면 종료
                if (!_sendQueue.TryDequeue(out var segment))
                    return;

                _isSending = true;
                _ = SendAsync(segment);
            }
        }

        private async Task SendAsync(ArraySegment<byte> segment)
        {
            try
            {
                int sent = 0; // 실제로 소켓에서 전송에 성공한 바이트수

                // 내가 보내야하는 세그먼트를 모두 보낼때까지 반복
                while (sent < segment.Count)
                {
                    // 데이터를 전송하겠다고 해서 전부 한번에 전송보장이 되는것이 아니다. 그래서 전송하고 남은거 또 보내야한다.
                    //ArraySegment<byte> remains = new ArraySegment<byte>(segment.Array, segment.Offset + sent, segment.Count - sent);

                    var token = new TaskCompletionSource<int>();
                    _sendArgs.SetBuffer(segment.Array, segment.Offset + sent, segment.Count - sent);
                    _sendArgs.UserToken = token;

                    bool pending = Socket.SendAsync(_sendArgs);

                    if (!pending)
                    {
                        OnSendCompleted(null, _sendArgs);
                    }

                    int justSent = await token.Task;

                    // 전송실패하면 닫음
                    if (justSent <= 0)
                    {
                        Dispose();
                        return;
                    }

                    sent += justSent;
                }
            }
            catch
            {
                Dispose();
            }
            finally
            {
                lock (_sendGate)
                {
                    _isSending = false;
                    Send(); // 대기중인 큐가 있으면 이어서 다음 프레임 처리
                }
            }
        }

        /// <summary>
        /// 데이터 수신 루프
        /// </summary>
        private async Task ReceiveLoopAsync()
        {
            while (IsConnected)
            {
                ArraySegment<byte> remainBufferSemgent = new ArraySegment<byte>(_receiveBuffer, _receiveBufferCount, _receiveBuffer.Length - _receiveBufferCount); // 남은 버퍼공간
                int bytesRead = await Socket.ReceiveAsync(remainBufferSemgent, SocketFlags.None);

                if (bytesRead <= 0)
                {
                    Dispose();
                    break;
                }

                _receiveBufferCount += bytesRead;
                ParsePackets();
            }
        }

        private void ParsePackets()
        {
            int offset = 0; // ReceiveBuffer 현재 탐색 위치

            while (true)
            {
                // 헤더 길이도 안되는 데이터는 파싱이 안되므로 데이터가 더 쌓일때까지 기다림
                if (_receiveBufferCount - offset < HEADER_SIZE)
                    break;

                // Header 는 body 길이
                int bodyLength = BitConverter.ToInt32(_receiveBuffer, offset);

                // 유효한 Body 인지
                if (bodyLength <= 0 || bodyLength > MAX_PACKET_SIZE)
                {
                    Dispose();
                    return;
                }

                // body 가 완전하게 다 도착하지 않았다면 다 도착할때까지 다시 검사
                if (_receiveBufferCount - offset - HEADER_SIZE < bodyLength)
                    break;

                // 완전한 패킷이 들어왔으므로 body 복사하여 처리
                byte[] body = new byte[bodyLength];
                Buffer.BlockCopy(_receiveBuffer, offset + HEADER_SIZE, body, 0, bodyLength);
                OnPacket(body);

                offset += HEADER_SIZE + bodyLength;
            }

            // 처리하고 남은 데이터를 버퍼 앞쪽으로 당김
            if (offset > 0)
            {
                Buffer.BlockCopy(_receiveBuffer, offset, _receiveBuffer, 0, _receiveBufferCount - offset);
                _receiveBufferCount -= offset;
            }
        }


        /// <summary>
        /// 수신한 패킷을 처리하는 함수
        /// </summary>
        protected abstract void OnPacket(byte[] body);


        private void InternalClose()
        {
            Console.WriteLine($"Disconnecting ... {Socket.RemoteEndPoint}");

            try
            {
                Socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                // TODO : 이미 닫혀있었을때의 예외처리
            }

            Socket.Close();
            OnDisconnected?.Invoke();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // 가비지 컬렉션에의해 이 객체의 소멸자가 호출되지 않도록 막음 
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    InternalClose();
                }
            }
        }
    }
}
