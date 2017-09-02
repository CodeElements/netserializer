/*
 * Copyright 2015 Tomi Valkeinen
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetSerializer.TypeSerializers
{
	internal sealed class PrimitivesSerializer : IStaticTypeSerializer
	{
		private static readonly Type[] Primitives =
		{
			typeof(bool),
			typeof(byte), typeof(sbyte),
			typeof(char),
			typeof(ushort), typeof(short),
			typeof(uint), typeof(int),
			typeof(ulong), typeof(long),
			typeof(float), typeof(double),
			typeof(string),
			typeof(DateTime),
			typeof(byte[]),
			typeof(decimal)
		};

		public bool Handles(TypeInfo type)
		{
			return Primitives.Contains(type.AsType());
		}

		public IEnumerable<Type> GetSubtypes(TypeInfo type)
		{
			return new Type[0];
		}

		public MethodInfo GetStaticWriter(TypeInfo type)
		{
			return NetSerializer.Primitives.GetWritePrimitive(type.AsType());
		}

		public MethodInfo GetStaticReader(TypeInfo type)
		{
			return NetSerializer.Primitives.GetReaderPrimitive(type.AsType());
		}

		public static IEnumerable<Type> GetSupportedTypes()
		{
			return Primitives;
		}
	}
}
