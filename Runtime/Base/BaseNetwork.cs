using System;
using System.Collections.Generic;
using System.Net;
using UniRx.Async;

namespace Com.Eyu.UnitySocketLibrary
{
    public class BaseNetwork : IDisposable
    {
        private readonly BaseService _service;

        private readonly Dictionary<long, NetworkSession> _sessions;

        private IMessagePacker _msgPacker;

        private IMessageDispatcher _msgDispatcher;

        //client
        public BaseNetwork(NetworkProtocol protocol, IMessagePacker msgPacker, int packetSize = Packet.PacketSizeLength4)
        {
            _msgPacker = msgPacker;
            _sessions = new Dictionary<long, NetworkSession>();
            switch (protocol)
            {
                case NetworkProtocol.TCP:
                    _service = new TService(packetSize);
                    break;
                case NetworkProtocol.WebSocket:
                    _service = new WService();
                    break;
            }
        }
        
        //accept(address i.e. 127.0.0.1:10002)
        public BaseNetwork(NetworkProtocol protocol, string address, IMessagePacker msgPacker, int packetSize = Packet.PacketSizeLength4)
        {
            _msgPacker = msgPacker;
            _sessions = new Dictionary<long, NetworkSession>();
            try
            {
                IPEndPoint ipEndPoint;
                switch (protocol)
                {
                    case NetworkProtocol.TCP:
                        ipEndPoint = NetworkHelper.ToIPEndPoint(address);
                        _service = new TService(packetSize, ipEndPoint, channel => { OnAccept(channel); });
                        break;
                    case NetworkProtocol.WebSocket:
                        var prefixes = address.Split(';');
                        _service = new WService(prefixes, channel => { OnAccept(channel); });
                        break;
                }
            }
            catch (Exception e)
            {
                throw new Exception($"NetworkComponent Awake Error {address}", e);
            }
        }
        
        protected NetworkSession OnAccept(BaseChannel channel)
        {
            var session = new NetworkSession(channel.Id, this, channel, _msgPacker, _msgDispatcher);
            _sessions.Add(session.Id, session);
            channel.Start();
            return session;
        }
        
        public NetworkSession CreateSession(long id, IPEndPoint ipEndPoint)
        {
            var channel = _service.ConnectChannel(ipEndPoint);
            var session = new NetworkSession(id, this, channel, _msgPacker, _msgDispatcher);
            _sessions.Add(session.Id, session);
            channel.Start();
            return session;
        }
		
        public NetworkSession CreateSession(long id, string address)
        {
            var channel = _service.ConnectChannel(address);
            var session = new NetworkSession(id, this, channel, _msgPacker, _msgDispatcher);
            _sessions.Add(session.Id, session);
            channel.Start();
            return session;
        }
            
        public void SetMsgDispatcher(IMessageDispatcher dispatcher)
        {
            _msgDispatcher = dispatcher;
        }
        
        public void RemoveSession(long id)
        {
            if (!_sessions.TryGetValue(id, out var session))
            {
                return;
            }
            _sessions.Remove(id);
            session.Dispose();
        }

        public NetworkSession GetSession(long id)
        {
            _sessions.TryGetValue(id, out var session);
            return session;
        }
        
        public UniTask<INetworkMessage> Call(INetworkMessage message, long sessionId)
        {
            var session = GetSession(sessionId);
            if (session != null)
            {
                return session.Call(message);
            }
            return UniTask.FromResult<INetworkMessage>(null);
        }

        public void Send(INetworkMessage message, long sessionId)
        {
            var session = GetSession(sessionId);
            session?.Send(message);
        }

        public void Update()
        {
            OneThreadSynchronizationContext.Instance.Update();
            _service?.Update();
        }

        public void Dispose()
        {
            _service.Dispose();
            
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }
            _sessions.Clear();
        }
    }
}