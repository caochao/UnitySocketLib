namespace Com.Eyu.UnitySocketLibrary
{
	public static class ErrorCode
	{
		public const int ERR_Success = 0;
		
		// 1-11004 是SocketError请看SocketError定义
		//-----------------------------------
		// 100000 以上，避免跟SocketError冲突
		public const int ERR_MyErrorCode = 100000;
		public const int ERR_NotFoundActor = 100002;
		public const int ERR_ActorNoMailBoxComponent = 100003;
		public const int ERR_ActorRemove = 100004;
		public const int ERR_PacketParserError = 100005;
		public const int ERR_ConnectGateKeyError = 100006;
		
		public const int ERR_RpcFail = 102001;
		public const int ERR_ReloadFail = 102003;
		public const int ERR_ActorLocationNotFound = 102004;
		public const int ERR_KcpCantConnect = 102005;
		public const int ERR_KcpChannelTimeout = 102006;
		public const int ERR_KcpRemoteDisconnect = 102007;
		public const int ERR_PeerDisconnect = 102008;
		public const int ERR_SocketCantSend = 102009;
		public const int ERR_SocketError = 102010;
		public const int ERR_KcpWaitSendSizeTooLarge = 102011;
		public const int ERR_ActorNotOnline = 102012;
		public const int ERR_ActorTimeout = 102013;
		public const int ERR_SessionSendOrRecvTimeout = 102014;

		public const int ERR_WebsocketPeerReset = 103001;
		public const int ERR_WebsocketMessageTooBig = 103002;
		public const int ERR_WebsocketError = 103003;
		public const int ERR_WebsocketConnectError = 103004;
		public const int ERR_WebsocketSendError = 103005;
		public const int ERR_WebsocketRecvError = 103006;
		
		
		public const int ERR_AccountOrPasswordError = 200102;
	}
}