using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using Microsoft.IO;
using UniRx.Async;

namespace Com.Eyu.UnitySocketLibrary
{
    public class WService: BaseService
    {
        private HttpListener _httpListener;
        public readonly RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();

        public WService(IEnumerable<string> prefixes, Action<BaseChannel> acceptCallback)
        {
            AcceptCallback += acceptCallback;
            _httpListener = new HttpListener();
            AcceptAsync(prefixes).Forget();
        }
        
        public WService()
        {
        }

        public override void Dispose()
        {
            IdChannels.Clear();
            _httpListener.Close();
            _httpListener = null;
        }
        
        public override BaseChannel ConnectChannel(IPEndPoint ipEndPoint)
        {
            throw new NotImplementedException();
        }

        public override BaseChannel ConnectChannel(string address)
        {
			var webSocket = new ClientWebSocket();
            var channel = new WChannel(webSocket, this);
            IdChannels[channel.Id] = channel;
            channel.ConnectAsync(address).Forget();
            return channel;
        }

        public override void Update()
        {
        }

        private async UniTaskVoid AcceptAsync(IEnumerable<string> prefixes)
        {
            try
            {
                foreach (var prefix in prefixes)
                {
                    _httpListener.Prefixes.Add(prefix);
                }
                _httpListener.Start();

                while (true)
                {
                    try
                    {
                        var httpListenerContext = await _httpListener.GetContextAsync();
                        var webSocketContext = await httpListenerContext.AcceptWebSocketAsync(null);
                        var channel = new WChannel(webSocketContext, this);
                        IdChannels[channel.Id] = channel;
                        OnAccept(channel);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
            catch (HttpListenerException e)
            {
                if (e.ErrorCode == 5)
                {
                    throw new Exception($"CMD管理员中输入: netsh http add urlacl url=http://*:8080/ user=Everyone", e);
                }

                Log.Error(e);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}