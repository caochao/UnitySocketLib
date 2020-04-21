﻿using System;
using System.IO;

namespace Com.Eyu.UnitySocketLibrary
{
	public enum ParserState
	{
		PacketSize,
		PacketBody
	}
	
	public static class Packet
	{
		public const int PacketSizeLength2 = 2;
		public const int PacketSizeLength4 = 4;
		public const int MinPacketSize = 2;
	}

	public class PacketParser
	{
		private readonly CircularBuffer _buffer;
		private int _packetSize;
		private ParserState _state;
		private readonly MemoryStream _memoryStream;
		private bool _isOk;
		private readonly int _packetSizeLength;

		public PacketParser(int packetSizeLength, CircularBuffer buffer, MemoryStream memoryStream)
		{
			_packetSizeLength = packetSizeLength;
			_buffer = buffer;
			_memoryStream = memoryStream;
		}

		public bool Parse()
		{
			if (_isOk)
			{
				return true;
			}

			var finish = false;
			while (!finish)
			{
				switch (_state)
				{
					case ParserState.PacketSize:
						if (_buffer.Length < _packetSizeLength)
						{
							finish = true;
						}
						else
						{
							//跳过包头
							_buffer.Read(_memoryStream.GetBuffer(), 0, _packetSizeLength);
							
							switch (_packetSizeLength)
							{
								case Packet.PacketSizeLength4:
									//包体大小，不包括包头字节数
									_packetSize = BitConverter.ToInt32(_memoryStream.GetBuffer(), 0);
									if (_packetSize > ushort.MaxValue * 16 || _packetSize < Packet.MinPacketSize)
									{
										throw new Exception($"recv packet size error, 可能是外网探测端口: {_packetSize}");
									}
									break;
								case Packet.PacketSizeLength2:
									//包体大小，不包括包头字节数
									_packetSize = BitConverter.ToUInt16(_memoryStream.GetBuffer(), 0);
									if (_packetSize > ushort.MaxValue || _packetSize < Packet.MinPacketSize)
									{
										throw new Exception($"recv packet size error:, 可能是外网探测端口: {_packetSize}");
									}
									break;
								default:
									throw new Exception("packet size byte count must be 2 or 4!");
							}
							_state = ParserState.PacketBody;
						}
						break;
					case ParserState.PacketBody:
						if (_buffer.Length < _packetSize)
						{
							finish = true;
						}
						else
						{
							//读取包体
							_memoryStream.Seek(0, SeekOrigin.Begin);
							_memoryStream.SetLength(_packetSize);
							var bytes = _memoryStream.GetBuffer();
							_buffer.Read(bytes, 0, _packetSize);
							_isOk = true;
							_state = ParserState.PacketSize;
							finish = true;
						}
						break;
				}
			}
			return _isOk;
		}

		public MemoryStream GetPacket()
		{
			_isOk = false;
			return _memoryStream;
		}
	}
}