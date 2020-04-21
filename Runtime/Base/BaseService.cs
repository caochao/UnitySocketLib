using System;
using System.Collections.Generic;
using System.Net;

namespace Com.Eyu.UnitySocketLibrary
{
	public enum NetworkProtocol
	{
		KCP,
		TCP,
		WebSocket,
	}

	public abstract class BaseService
	{
		protected readonly Dictionary<long, BaseChannel> IdChannels = new Dictionary<long, BaseChannel>();
		
		public BaseChannel GetChannel(long id)
		{
			IdChannels.TryGetValue(id, out var channel);
			return channel;
		}

		public virtual void RemoveChannel(long id)
		{
			IdChannels.Remove(id);
		}

		private Action<BaseChannel> _acceptCallback;

		public event Action<BaseChannel> AcceptCallback
		{
			add => _acceptCallback += value;
			remove => _acceptCallback -= value;
		}
		
		protected void OnAccept(BaseChannel channel)
		{
			_acceptCallback.Invoke(channel);
		}

		public abstract BaseChannel ConnectChannel(IPEndPoint ipEndPoint);
		
		public abstract BaseChannel ConnectChannel(string address);

		public abstract void Update();

		public abstract void Dispose();
	}
}