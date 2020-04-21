# UnitySocketLib
A server/client side Socket/WebSocket library integrated with google protobuf(v3.11.4) for unity3d project.

## Features:
- 1.The same codes and same workflow for server/client side use.
- 2.Extensible socket data packer/unpacker. Built-in Google protobuf packer, but easy to implement json/string packer.
- 3.Socket packet handler based on messaging system.
- 4.Multiple sessions/channels.

## How to use:
```c#
    public enum Server
    {
        LoginServer,
        GameServer,
    }
    
    public class NetworkController
    {
        private BaseNetwork _network;

        public NetworkController()
        {
            _network = new BaseNetwork(NetworkProtocol.TCP, new ProtobufPacker());
            _network.SetMsgDispatcher(new NetworkMsgDispatcher());
        }

        public void CreateSession(string address, Server server = Server.GameServer)
        {
            _network.CreateSession((long)server, address);
        }
        
        public UniTask<INetworkMessage> Call(INetworkMessage message, Server server = Server.GameServer)
        {
            return _network.Call(message, (long) server);
        }

        public void Send(INetworkMessage message, Server server = Server.GameServer)
        {
            _network.Send(message, (long) server);
        }

        public void OnUpdate()
        {
            _network.Update();
        }

        public void OnDestroy()
        {
            _network.Dispose();
            _network = null;
        }
    }
```
