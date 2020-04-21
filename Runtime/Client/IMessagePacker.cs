﻿using System.IO;
using Google.Protobuf;

namespace Com.Eyu.UnitySocketLibrary
{
	public interface IMessagePacker
	{
		void SerializeTo(IMessage message, MemoryStream stream);
		byte[] SerializeTo(IMessage message);

		INetworkMessage DeserializeFrom(IMessage message, byte[] bytes, int index, int count);
		INetworkMessage DeserializeFrom(IMessage message, MemoryStream stream);
	}
}
