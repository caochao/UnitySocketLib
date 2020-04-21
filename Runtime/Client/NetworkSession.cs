using System;
using System.Collections.Generic;
using System.IO;
using Google.Protobuf;
using UniRx.Async;

namespace Com.Eyu.UnitySocketLibrary
{
	public sealed class NetworkSession
	{
		private bool _isDisposed;
		private static int ReqId { get; set; }
		public long Id { get; }
		private readonly BaseNetwork _network;
		private readonly IMessageDispatcher _msgDispatcher;
		private readonly IMessagePacker _msgPacker;
		private readonly BaseChannel _channel;

		private readonly Dictionary<int, Action<INetworkMessage>> _requestCallback;

		private long _lastRecvTime;
		private long _lastSendTime;

		public int Error
		{
			get
			{
				return _channel.Error;
			}
			set
			{
				_channel.Error = value;
			}
		}

		public NetworkSession(long id, BaseNetwork network, BaseChannel channel, IMessagePacker packer, IMessageDispatcher dispatcher)
		{
			Id = id;
			_network = network;
			_msgPacker = packer;
			_msgDispatcher = dispatcher;
			var timeNow = TimeHelper.Now();
			_lastRecvTime = timeNow;
			_lastSendTime = timeNow;
			_requestCallback = new Dictionary<int, Action<INetworkMessage>>();
			
			_channel = channel;
			_channel.ErrorCallback += (c, e) =>
			{
				Log.Info($"session error, sessionId: {Id},  ErrorCode: {e}");
				_network.RemoveSession(id);
			};
			_channel.ReadCallback += OnRead;
		}
		
		public void Dispose()
		{
			if (_isDisposed)
			{
				return;
			}
			_isDisposed = true;
			_channel.Dispose();
			_requestCallback.Clear();
		}

		private MemoryStream Stream => _channel.Stream;

		private void OnRead(MemoryStream memoryStream)
		{
			try
			{
				OnRecvPacket(memoryStream);
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
		}

		private void OnRecvPacket(MemoryStream memoryStream)
		{
			INetworkMessage message = null;
			try
			{
#if SERVER
				var instance = MessagePool.Instance.Fetch<C2S>();
#else
				var instance = MessagePool.Instance.Fetch<S2C>();
#endif
				message = _msgPacker.DeserializeFrom(instance, memoryStream);
			}
			catch (Exception e)
			{
				// 出现任何消息解析异常都要断开Session，防止客户端伪造消息
				Log.Error($"command: {message.Cmd}, error: {e}");
				Error = ErrorCode.ERR_PacketParserError;
				_network.RemoveSession(Id);
				return;
			}
			
			_lastRecvTime = TimeHelper.Now();
			if (_requestCallback.TryGetValue(message.ReqId, out var action))
			{
				action(message);
				_requestCallback.Remove(message.ReqId);
			}
			else
			{
				_msgDispatcher?.Dispatch(this, message);
			}
		}

		public UniTask<INetworkMessage> Call(INetworkMessage message)
		{
			var reqId = ++ReqId;
			var tcs = new UniTaskCompletionSource<INetworkMessage>();
			_requestCallback[reqId] = response =>
			{
				tcs.TrySetResult(response);
			};

			message.ReqId = reqId;
			Send(message as IMessage);
			return tcs.Task;
		}

		public void Send(INetworkMessage message)
		{
			Send(message as IMessage);
		}

		private void Send(IMessage message)
		{
			if (_isDisposed)
			{
				throw new Exception("session已经被Dispose了");
			}

			_lastSendTime = TimeHelper.Now();

			//包头(4字节，内容表示包体大小) + 包体
			var stream = Stream;
			stream.Seek(0, SeekOrigin.Begin);
			_msgPacker.SerializeTo(message, stream);

			_channel.Send(stream);
		}
	}
}