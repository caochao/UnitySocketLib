using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Com.Eyu.UnitySocketLibrary
{
	/// <summary>
	/// 封装Socket,将回调push到主线程处理
	/// </summary>
	public sealed class TChannel: BaseChannel
	{
		private Socket _socket;
		private SocketAsyncEventArgs _innArgs = new SocketAsyncEventArgs();
		private SocketAsyncEventArgs _outArgs = new SocketAsyncEventArgs();

		private readonly CircularBuffer _recvBuffer = new CircularBuffer();
		private readonly CircularBuffer _sendBuffer = new CircularBuffer();

		private readonly MemoryStream _memoryStream;

		private bool _isConnected;

		private readonly PacketParser _parser;

		private readonly byte[] _packetSizeCache;

		private readonly IPEndPoint _remoteIpEndPoint;
		
		//connect
		public TChannel(IPEndPoint ipEndPoint, TService service): base(service, ChannelType.Connect)
		{
			var packetSize = service.PacketSizeLength;
			_packetSizeCache = new byte[packetSize];
			_memoryStream = service.MemoryStreamManager.GetStream("message", ushort.MaxValue);
			
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_socket.NoDelay = true;
			_parser = new PacketParser(packetSize, _recvBuffer, _memoryStream);
			_innArgs.Completed += OnComplete;
			_outArgs.Completed += OnComplete;

			RemoteAddress = ipEndPoint.ToString();
			_remoteIpEndPoint = ipEndPoint;
			_isConnected = false;
			IsSending = false;
		}
		
		//accept
		public TChannel(Socket socket, TService service): base(service, ChannelType.Accept)
		{
			var packetSize = service.PacketSizeLength;
			_packetSizeCache = new byte[packetSize];
			_memoryStream = service.MemoryStreamManager.GetStream("message", ushort.MaxValue);
			
			_socket = socket;
			socket.NoDelay = true;
			_parser = new PacketParser(packetSize, _recvBuffer, _memoryStream);
			_innArgs.Completed += OnComplete;
			_outArgs.Completed += OnComplete;

			RemoteAddress = socket.RemoteEndPoint.ToString();
			_remoteIpEndPoint = (IPEndPoint)socket.RemoteEndPoint;
			_isConnected = true;
			IsSending = false;
		}

		public override void Dispose()
		{
			Service.RemoveChannel(Id);
			_socket.Close();
			_innArgs.Dispose();
			_outArgs.Dispose();
			_innArgs = null;
			_outArgs = null;
			_socket = null;
			_memoryStream.Dispose();
		}

		public override void Start()
		{
			if (ChannelType == ChannelType.Accept)
			{
				StartRecv();
			}
			else
			{
				ConnectAsync(_remoteIpEndPoint);
			}
		}
		
		private TService GetService()
		{
			return (TService)Service;
		}

		public override MemoryStream Stream
		{
			get
			{
				return _memoryStream;
			}
		}
		
		public override void Send(MemoryStream stream)
		{
			switch (GetService().PacketSizeLength)
			{
				case Packet.PacketSizeLength4:
					if (stream.Length > ushort.MaxValue * 16)
					{
						throw new Exception($"send packet too large: {stream.Length}");
					}
					_packetSizeCache.WriteTo(0, (int) stream.Length);
					break;
				case Packet.PacketSizeLength2:
					if (stream.Length > ushort.MaxValue)
					{
						throw new Exception($"send packet too large: {stream.Length}");
					}
					_packetSizeCache.WriteTo(0, (ushort) stream.Length);
					break;
				default:
					throw new Exception("packet size must be 2 or 4!");
			}

			_sendBuffer.Write(_packetSizeCache, 0, _packetSizeCache.Length);
			_sendBuffer.Write(stream);

			GetService().MarkNeedStartSend(Id);
		}

		private void OnComplete(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Connect:
					OneThreadSynchronizationContext.Instance.Post(OnConnectComplete, e);
					break;
				case SocketAsyncOperation.Receive:
					OneThreadSynchronizationContext.Instance.Post(OnRecvComplete, e);
					break;
				case SocketAsyncOperation.Send:
					OneThreadSynchronizationContext.Instance.Post(OnSendComplete, e);
					break;
				case SocketAsyncOperation.Disconnect:
					OneThreadSynchronizationContext.Instance.Post(OnDisconnectComplete, e);
					break;
				default:
					throw new Exception($"socket error: {e.LastOperation}");
			}
		}

		private void ConnectAsync(IPEndPoint ipEndPoint)
		{
			_outArgs.RemoteEndPoint = ipEndPoint;
			if (_socket.ConnectAsync(_outArgs))
			{
				return;
			}
			OnConnectComplete(_outArgs);
		}

		private void OnConnectComplete(object o)
		{
			if (_socket == null)
			{
				return;
			}
			var e = o as SocketAsyncEventArgs;
			
			if (e.SocketError != SocketError.Success)
			{
				OnError((int) e.SocketError);
				return;
			}

			e.RemoteEndPoint = null;
			_isConnected = true;
			StartRecv();
			GetService().MarkNeedStartSend(Id);
		}

		private void OnDisconnectComplete(object o)
		{
			var e = o as SocketAsyncEventArgs;
			OnError((int)e.SocketError);
		}

		private void StartRecv()
		{
			var size = _recvBuffer.ChunkSize - _recvBuffer.LastIndex;
			RecvAsync(_recvBuffer.Last, _recvBuffer.LastIndex, size);
		}

		private void RecvAsync(byte[] buffer, int offset, int count)
		{
			try
			{
				_innArgs.SetBuffer(buffer, offset, count);
			}
			catch (Exception e)
			{
				throw new Exception($"socket set buffer error: {buffer.Length}, {offset}, {count}", e);
			}
			
			if (_socket.ReceiveAsync(_innArgs))
			{
				return;
			}
			OnRecvComplete(_innArgs);
		}

		private void OnRecvComplete(object o)
		{
			if (_socket == null)
			{
				return;
			}
			var e = o as SocketAsyncEventArgs;

			if (e.SocketError != SocketError.Success)
			{
				OnError((int)e.SocketError);
				return;
			}

			if (e.BytesTransferred == 0)
			{
				OnError(ErrorCode.ERR_PeerDisconnect);
				return;
			}

			_recvBuffer.LastIndex += e.BytesTransferred;
			if (_recvBuffer.LastIndex == _recvBuffer.ChunkSize)
			{
				_recvBuffer.AddLast();
				_recvBuffer.LastIndex = 0;
			}

			// 收到消息回调
			while (true)
			{
				try
				{
					if (!_parser.Parse())
					{
						break;
					}
				}
				catch (Exception ee)
				{
					Log.Error($"ip: {RemoteAddress} {ee}");
					OnError(ErrorCode.ERR_SocketError);
					return;
				}

				try
				{
					OnRead(_parser.GetPacket());
				}
				catch (Exception ee)
				{
					Log.Error(ee);
				}
			}

			if (_socket == null)
			{
				return;
			}
			
			StartRecv();
		}

		public bool IsSending { get; private set; }

		public void StartSend()
		{
			if(!_isConnected)
			{
				return;
			}
			
			// 没有数据需要发送
			if (_sendBuffer.Length == 0)
			{
				IsSending = false;
				return;
			}

			IsSending = true;

			var sendSize = _sendBuffer.ChunkSize - _sendBuffer.FirstIndex;
			if (sendSize > _sendBuffer.Length)
			{
				sendSize = (int)_sendBuffer.Length;
			}

			SendAsync(_sendBuffer.First, _sendBuffer.FirstIndex, sendSize);
		}

		private void SendAsync(byte[] buffer, int offset, int count)
		{
			try
			{
				_outArgs.SetBuffer(buffer, offset, count);
			}
			catch (Exception e)
			{
				throw new Exception($"socket set buffer error: {buffer.Length}, {offset}, {count}", e);
			}
			if (_socket.SendAsync(_outArgs))
			{
				return;
			}
			OnSendComplete(_outArgs);
		}

		private void OnSendComplete(object o)
		{
			if (_socket == null)
			{
				return;
			}
			var e = o as SocketAsyncEventArgs;

			if (e.SocketError != SocketError.Success)
			{
				OnError((int)e.SocketError);
				return;
			}
			
			if (e.BytesTransferred == 0)
			{
				OnError(ErrorCode.ERR_PeerDisconnect);
				return;
			}
			
			_sendBuffer.FirstIndex += e.BytesTransferred;
			if (_sendBuffer.FirstIndex == _sendBuffer.ChunkSize)
			{
				_sendBuffer.FirstIndex = 0;
				_sendBuffer.RemoveFirst();
			}
			
			StartSend();
		}
	}
}