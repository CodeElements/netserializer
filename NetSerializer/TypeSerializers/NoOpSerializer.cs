/*
 * Copyright 2015 Tomi Valkeinen
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetSerializer.TypeSerializers
{
	/// <summary>
	///     A "no-op" TypeSerializer which can be used to make the NetSerializer ignore fields of certain type.
	///     For example, Delegates cannot be serializer by default, and NoOpSerializer could be used to ignore all subclasses
	///     of Delegate
	/// </summary>
	internal sealed class NoOpSerializer : IStaticTypeSerializer
	{
		private readonly bool _handleSubclasses;
		private readonly TypeInfo[] _types;

		public NoOpSerializer(IEnumerable<TypeInfo> types, bool handleSubclasses)
		{
			_types = types.ToArray();
			_handleSubclasses = handleSubclasses;
		}

		public bool Handles(TypeInfo type)
		{
			if (_handleSubclasses)
				return _types.Any(x => type.IsSubclassOf(x.AsType()));
			return _types.Contains(type);
		}

		public IEnumerable<Type> GetSubtypes(TypeInfo type)
		{
			return new Type[0];
		}

		public MethodInfo GetStaticWriter(TypeInfo type)
		{
			return GetType().GetTypeInfo().GetDeclaredMethod("Serialize");
		}

		public MethodInfo GetStaticReader(TypeInfo type)
		{
			return GetType().GetTypeInfo().GetDeclaredMethod("Deserialize");
		}

		public static void Serialize(Serializer serializer, Stream stream, object ob)
		{
		}

		public static void Deserialize(Serializer serializer, Stream stream, out object ob)
		{
			ob = null;
		}
	}
}
