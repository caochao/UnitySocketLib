using System;
using System.IO;

namespace Com.Eyu.UnitySocketLibrary
{
    public enum ChannelType
    {
        Connect,
        Accept,
    }

    public abstract class BaseChannel : IncreasedId
    {
        protected ChannelType ChannelType { get; }

        protected BaseService Service { get; }

        public abstract MemoryStream Stream { get; }
		
        public int Error { get; set; }

        protected string RemoteAddress { get; set; }

        public virtual void Start()
        {
        }

        private Action<BaseChannel, int> _errorCallback;
		
        public event Action<BaseChannel, int> ErrorCallback
        {
            add => _errorCallback += value;
            remove => _errorCallback -= value;
        }
		
        private Action<MemoryStream> _readCallback;

        public event Action<MemoryStream> ReadCallback
        {
            add => _readCallback += value;
            remove => _readCallback -= value;
        }
		
        protected void OnRead(MemoryStream memoryStream)
        {
            _readCallback.Invoke(memoryStream);
        }

        protected void OnError(int e)
        {
            Error = e;
            _errorCallback?.Invoke(this, e);
        }

        protected BaseChannel(BaseService service, ChannelType channelType)
        {
            ChannelType = channelType;
            Service = service;
        }
		
        public abstract void Send(MemoryStream stream);

        public abstract void Dispose();
    }
}