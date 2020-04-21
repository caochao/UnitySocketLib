using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Microsoft.IO;

namespace Com.Eyu.UnitySocketLibrary
{
	public sealed class TService : BaseService
	{
		private readonly SocketAsyncEventArgs _innArgs = new SocketAsyncEventArgs();
		private Socket _acceptor;
		
		public readonly RecyclableMemoryStreamManager MemoryStreamManager = new RecyclableMemoryStreamManager();
		
		private readonly List<long> _needStartSendChannel = new List<long>();
		
		public int PacketSizeLength { get; }
		
		//accept
		public TService(int packetSizeLength, IPEndPoint ipEndPoint, Action<BaseChannel> acceptCallback)
		{
			PacketSizeLength = packetSizeLength;
			AcceptCallback += acceptCallback;
			
			_acceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_acceptor.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			_innArgs.Completed += OnComplete;
			
			_acceptor.Bind(ipEndPoint);
			_acceptor.Listen(1000);

			AcceptAsync();
		}

		//client
		public TService(int packetSizeLength)
		{
			PacketSizeLength = packetSizeLength;
		}
		
		public override void Dispose()
		{
			IdChannels.Clear();
			_needStartSendChannel.Clear();
			_acceptor?.Close();
			_acceptor = null;
			_innArgs.Dispose();
		}

		private void OnComplete(object sender, SocketAsyncEventArgs e)
		{
			switch (e.LastOperation)
			{
				case SocketAsyncOperation.Accept:
					OneThreadSynchronizationContext.Instance.Post(OnAcceptComplete, e);
					break;
				default:
					throw new Exception($"socket accept error: {e.LastOperation}");
			}
		}

		private void AcceptAsync()
		{
			_innArgs.AcceptSocket = null;
			if (_acceptor.AcceptAsync(_innArgs))
			{
				return;
			}
			OnAcceptComplete(_innArgs);
		}

		private void OnAcceptComplete(object o)
		{
			if (_acceptor == null)
			{
				return;
			}
			var e = o as SocketAsyncEventArgs;
			
			if (e.SocketError != SocketError.Success)
			{
				Log.Error($"accept error {e.SocketError}");
				AcceptAsync();
				return;
			}
			var channel = new TChannel(e.AcceptSocket, this);
			IdChannels[channel.Id] = channel;

			try
			{
				OnAccept(channel);
			}
			catch (Exception exception)
			{
				Log.Error(exception);
			}

			if (_acceptor == null)
			{
				return;
			}
			
			AcceptAsync();
		}

		public override BaseChannel ConnectChannel(IPEndPoint ipEndPoint)
		{
			var channel = new TChannel(ipEndPoint, this);
			IdChannels[channel.Id] = channel;
			return channel;
		}

		public override BaseChannel ConnectChannel(string address)
		{
			var ipEndPoint = NetworkHelper.ToIPEndPoint(address);
			return ConnectChannel(ipEndPoint);
		}

		public override void RemoveChannel(long id)
		{
			base.RemoveChannel(id);
			_needStartSendChannel.Remove(id);
		}
		
		public void MarkNeedStartSend(long id)
		{
			_needStartSendChannel.Add(id);
		}

		public override void Update()
		{
			foreach (var id in _needStartSendChannel)
			{
				if (!IdChannels.TryGetValue(id, out var channel))
				{
					continue;
				}
				var tChannel = channel as TChannel; 
				if (tChannel.IsSending)
				{
					continue;
				}

				try
				{
					tChannel.StartSend();
				}
				catch (Exception e)
				{
					Log.Error(e);
				}
			}
			
			_needStartSendChannel.Clear();
		}
	}
}