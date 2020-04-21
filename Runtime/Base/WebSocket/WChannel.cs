using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using UniRx.Async;

namespace Com.Eyu.UnitySocketLibrary
{
    public class WChannel: BaseChannel
    {
        private HttpListenerWebSocketContext WebSocketContext { get; }

        private readonly WebSocket _webSocket;

        private readonly Queue<byte[]> _queue = new Queue<byte[]>();

        private bool _isSending;
        private bool _isConnected;
        private bool _isRemoved;

        private readonly MemoryStream _memoryStream;

        private readonly MemoryStream _recvStream;

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public WChannel(HttpListenerWebSocketContext webSocketContext, BaseService service): base(service, ChannelType.Accept)
        {
            WebSocketContext = webSocketContext;

            _webSocket = webSocketContext.WebSocket;

            _memoryStream = GetService().MemoryStreamManager.GetStream("message", ushort.MaxValue);
            _recvStream = GetService().MemoryStreamManager.GetStream("message", ushort.MaxValue);

            _isConnected = true;
        }

        public WChannel(WebSocket webSocket, BaseService service): base(service, ChannelType.Connect)
        {
            _webSocket = webSocket;

            _memoryStream = GetService().MemoryStreamManager.GetStream("message", ushort.MaxValue);
            _recvStream = GetService().MemoryStreamManager.GetStream("message", ushort.MaxValue);

            _isConnected = false;
        }

        public override void Dispose()
        {
            Service.RemoveChannel(Id);
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            _webSocket.Dispose();
            _memoryStream.Dispose();
        }

        public override MemoryStream Stream => _memoryStream;

        public override void Start()
        {
            if (!_isConnected)
            {
                return;
            }

            StartRecv().Forget();
            StartSend().Forget();
        }

        private WService GetService()
        {
            return (WService) Service;
        }

        public async UniTaskVoid ConnectAsync(string url)
        {
            try
            {
                await ((ClientWebSocket) _webSocket).ConnectAsync(new Uri(url), _cancellationTokenSource.Token);
                _isConnected = true;
                Start();
            }
            catch (Exception e)
            {
                Log.Error(e);
                OnError(ErrorCode.ERR_WebsocketConnectError);
            }
        }

        public override void Send(MemoryStream stream)
        {
            var bytes = new byte[stream.Length];
            Array.Copy(stream.GetBuffer(), bytes, bytes.Length);
            _queue.Enqueue(bytes);

            if (_isConnected)
            {
                StartSend().Forget();
            }
        }

        private async UniTaskVoid StartSend()
        {
            if (_isRemoved)
            {
                return;
            }

            try
            {
                if (_isSending)
                {
                    return;
                }

                _isSending = true;

                while (true)
                {
                    if (_queue.Count == 0)
                    {
                        _isSending = false;
                        return;
                    }

                    var bytes = _queue.Dequeue();
                    try
                    {
                        await _webSocket.SendAsync(new ArraySegment<byte>(bytes, 0, bytes.Length), WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
                        if (_isRemoved)
                        {
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                        OnError(ErrorCode.ERR_WebsocketSendError);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        private async UniTaskVoid StartRecv()
        {
            if (_isRemoved)
            {
                return;
            }

            try
            {
                while (true)
                {
#if SERVER
                    ValueWebSocketReceiveResult receiveResult;
#else
                    WebSocketReceiveResult receiveResult;
#endif
                    var receiveCount = 0;
                    do
                    {
#if SERVER
                        receiveResult = await webSocket.ReceiveAsync(
                            new Memory<byte>(recvStream.GetBuffer(), receiveCount, recvStream.Capacity - receiveCount),
                            cancellationTokenSource.Token);
#else
                        receiveResult = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(_recvStream.GetBuffer(), receiveCount, _recvStream.Capacity - receiveCount), 
                            _cancellationTokenSource.Token);
#endif
                        if (_isRemoved)
                        {
                            return;
                        }

                        receiveCount += receiveResult.Count;
                    }
                    while (!receiveResult.EndOfMessage);

                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        OnError(ErrorCode.ERR_WebsocketPeerReset);
                        return;
                    }

                    if (receiveResult.Count > ushort.MaxValue)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig, $"message too big: {receiveResult.Count}",
                            _cancellationTokenSource.Token);
                        OnError(ErrorCode.ERR_WebsocketMessageTooBig);
                        return;
                    }

                    _recvStream.SetLength(receiveResult.Count);
                    OnRead(_recvStream);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                OnError(ErrorCode.ERR_WebsocketRecvError);
            }
        }
    }
}