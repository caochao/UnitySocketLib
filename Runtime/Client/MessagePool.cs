﻿using System;
#if !SERVER
using System.Collections.Generic;		
#endif

namespace Com.Eyu.UnitySocketLibrary
{
	public class MessagePool
	{
		public static MessagePool Instance { get; } = new MessagePool();
		private readonly Dictionary<Type, Queue<object>> dictionary = new Dictionary<Type, Queue<object>>();

		public object Fetch(Type type)
		{
			if (!dictionary.TryGetValue(type, out var queue))
			{
				queue = new Queue<object>();
				dictionary.Add(type, queue);
			}

			return queue.Count > 0 ? queue.Dequeue() : Activator.CreateInstance(type);
		}

		public T Fetch<T>() where T : class
		{
			return (T) Fetch(typeof (T));
		}

		public void Recycle(object obj)
		{
			var type = obj.GetType();
			if (!dictionary.TryGetValue(type, out var queue))
			{
				queue = new Queue<object>();
				dictionary.Add(type, queue);
			}
			queue.Enqueue(obj);
		}
	}
}