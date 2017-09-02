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

namespace NetSerializer
{
	/// <summary>
	///     Threadsafe TypeID -> TypeData list, which supports lockless reading.
	/// </summary>
	internal class TypeIdList
	{
		private const int InitialLength = 256;
		private TypeData[] _array;
		private readonly object _writeLock = new object();

		public TypeIdList()
		{
			_array = new TypeData[InitialLength];
		}

		public TypeData this[uint idx]
		{
			get => _array[idx];

			set
			{
				lock (_writeLock)
				{
					Debug.Assert(value.TypeId == idx);

					if (idx >= _array.Length)
					{
						var newArray = new TypeData[NextPowOf2(idx + 1)];
						Array.Copy(_array, newArray, _array.Length);
						_array = newArray;
					}

					Debug.Assert(_array[idx] == null);

					_array[idx] = value;
				}
			}
		}

		public bool ContainsTypeId(uint typeId)
		{
			return typeId < _array.Length && _array[typeId] != null;
		}

		private uint NextPowOf2(uint v)
		{
			v--;
			v |= v >> 1;
			v |= v >> 2;
			v |= v >> 4;
			v |= v >> 8;
			v |= v >> 16;
			v++;
			return v;
		}

		public SortedList<uint, Type> ToSortedList()
		{
			var list = new SortedList<uint, Type>();

			lock (_writeLock)
			{
				for (uint i = 0; i < _array.Length; ++i)
				{
					var td = _array[i];

					if (td == null)
						continue;

					list.Add(i, td.Type);
				}
			}

			return list;
		}
	}
}
