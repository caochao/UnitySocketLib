﻿using System.IO;
using Google.Protobuf;

namespace Com.Eyu.UnitySocketLibrary
{
	public class ProtobufPacker : IMessagePacker
	{
		public void SerializeTo(IMessage message, MemoryStream stream)
		{
			message.WriteTo(stream);
		}
		
		public byte[] SerializeTo(IMessage message)
		{
			return message.ToByteArray();
		}

		public INetworkMessage DeserializeFrom(IMessage message, byte[] bytes, int index, int count)
		{
			message.MergeFrom(bytes, index, count);
			return message as INetworkMessage;
		}

		public INetworkMessage DeserializeFrom(IMessage message, MemoryStream stream)
		{
			// 这个message可以从池中获取，减少gc
			message.MergeFrom(stream.GetBuffer(), (int)stream.Position, (int)stream.Length);
			return message as INetworkMessage;
		}
	}
}
