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
using System.Reflection;

namespace NetSerializer.TypeSerializers
{
	internal sealed class EnumSerializer : IStaticTypeSerializer
	{
		public bool Handles(TypeInfo type)
		{
			return type.IsEnum;
		}

		public IEnumerable<Type> GetSubtypes(TypeInfo type)
		{
			var underlyingType = Enum.GetUnderlyingType(type.AsType());

			return new[] {underlyingType};
		}

		public MethodInfo GetStaticWriter(TypeInfo type)
		{
			Debug.Assert(type.IsEnum);

			var underlyingType = Enum.GetUnderlyingType(type.AsType());

			return Primitives.GetWritePrimitive(underlyingType);
		}

		public MethodInfo GetStaticReader(TypeInfo type)
		{
			Debug.Assert(type.IsEnum);

			var underlyingType = Enum.GetUnderlyingType(type.AsType());

			return Primitives.GetReaderPrimitive(underlyingType);
		}
	}
}
