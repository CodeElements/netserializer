/*
 * Copyright 2015 Tomi Valkeinen
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NetSerializer
{
	/// <summary>
	///     Threadsafe Type -> T dictionary, which supports lockless reading.
	/// </summary>
	internal class TypeDictionary
	{
		private const int InitialListSize = 1;
		private const float LoadLimit = 0.50f;
		private const int InitialLength = 256;
		private readonly object _writeLock = new object();

		private Pair[][] _buckets;
		private int _numItems;

		public TypeDictionary()
		{
			var numBuckets = (int) (InitialLength * (1.0f / LoadLimit));
			_buckets = new Pair[numBuckets][];
		}

		public TypeData this[Type key]
		{
			get
			{
				var buckets = Volatile.Read(ref _buckets);

				var idx = Hash(key, buckets.Length);

				var arr = Volatile.Read(ref buckets[idx]);
				if (arr == null)
					throw new KeyNotFoundException(string.Format("Type not found {0}", key));

				for (var i = 0; i < arr.Length; ++i)
					if (arr[i].Key == key)
						return arr[i].Value;

				throw new KeyNotFoundException(string.Format("Type not found {0}", key));
			}

			set
			{
				lock (_writeLock)
				{
					Debug.Assert(ContainsKey(key) == false);

					if (_numItems >= _buckets.Length * LoadLimit)
					{
						var newBuckets = new Pair[_buckets.Length * 2][];

						foreach (var list in _buckets.Where(l => l != null))
						foreach (var pair in list.Where(p => p.Key != null))
							Add(newBuckets, pair.Key, pair.Value);

						Volatile.Write(ref _buckets, newBuckets);
					}

					Add(_buckets, key, value);

					_numItems++;
				}
			}
		}

		public bool ContainsKey(Type key)
		{
			return TryGetValue(key, out var _);
		}

		public bool TryGetValue(Type key, out TypeData value)
		{
			var buckets = Volatile.Read(ref _buckets);

			var idx = Hash(key, buckets.Length);

			var arr = Volatile.Read(ref buckets[idx]);
			if (arr == null)
				goto not_found;

			for (var i = 0; i < arr.Length; ++i)
				if (arr[i].Key == key)
				{
					value = arr[i].Value;
					return true;
				}

			not_found:
			value = null;
			return false;
		}

		private static void Add(Pair[][] buckets, Type key, TypeData value)
		{
			var idx = Hash(key, buckets.Length);

			var arr = buckets[idx];
			if (arr == null)
				buckets[idx] = arr = new Pair[InitialListSize];

			for (var i = 0; i < arr.Length; ++i)
				if (arr[i].Key == null)
				{
					arr[i] = new Pair(key, value);
					return;
				}

			var newArr = new Pair[arr.Length * 2];
			Array.Copy(arr, newArr, arr.Length);
			newArr[arr.Length] = new Pair(key, value);
			buckets[idx] = newArr;
		}

		private static int Hash(Type key, int bucketsLen)
		{
			var h = (uint) RuntimeHelpers.GetHashCode(key);
			h %= (uint) bucketsLen;
			return (int) h;
		}

		public Dictionary<Type, uint> ToDictionary()
		{
			var map = new Dictionary<Type, uint>(_numItems);

			foreach (var list in _buckets)
			{
				if (list == null)
					continue;

				foreach (var pair in list)
				{
					if (pair.Key == null)
						continue;

					var td = pair.Value;

					map[td.Type] = td.TypeId;
				}
			}

			return map;
		}


		[Conditional("DEBUG")]
		public void DebugDump()
		{
			var occupied = _buckets.Count(i => i != null);

			Console.WriteLine("bucket arr len {0}, items {1}, occupied buckets {2}", _buckets.Length, _numItems, occupied);

			var countmap = new Dictionary<int, int>();
			foreach (var list in _buckets)
			{
				if (list == null)
					continue;

				var c = list.TakeWhile(p => p.Key != null).Count();
				if (countmap.ContainsKey(c) == false)
					countmap[c] = 0;
				countmap[c]++;
			}

			foreach (var kvp in countmap.OrderBy(kvp => kvp.Key))
				Console.WriteLine("{0}: {1}", kvp.Key, kvp.Value);
		}

		private struct Pair
		{
			public Pair(Type key, TypeData value)
			{
				Key = key;
				Value = value;
			}

			public readonly Type Key;
			public readonly TypeData Value;
		}
	}
}
