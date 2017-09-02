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
using System.Reflection.Emit;

namespace NetSerializer.TypeSerializers
{
	internal sealed class NullableSerializer : IDynamicTypeSerializer
	{
		public bool Handles(TypeInfo type)
		{
			if (!type.IsGenericType)
				return false;

			var genTypeDef = type.GetGenericTypeDefinition();

			return genTypeDef == typeof(Nullable<>);
		}

		public IEnumerable<Type> GetSubtypes(TypeInfo type)
		{
			var genArgs = type.GenericTypeArguments;

			return new[] {typeof(bool), genArgs[0]};
		}

		public void GenerateWriterMethod(Serializer serializer, TypeInfo type, ILGenerator il)
		{
			var valueType = type.GenericTypeArguments[0];

			var noValueLabel = il.DefineLabel();

			var getHasValue = type.GetDeclaredProperty("HasValue").GetMethod;
			var getValue = type.GetDeclaredProperty("Value").GetMethod;

			var data = serializer.GetIndirectData(valueType);

			il.Emit(OpCodes.Ldarg_1); // Stream
			il.Emit(OpCodes.Ldarga_S, 2); // &value
			il.Emit(OpCodes.Call, getHasValue);
			il.Emit(OpCodes.Call, serializer.GetDirectWriter(typeof(bool)));

			il.Emit(OpCodes.Ldarga_S, 2); // &value
			il.Emit(OpCodes.Call, getHasValue);
			il.Emit(OpCodes.Brfalse_S, noValueLabel);

			if (data.WriterNeedsInstance)
				il.Emit(OpCodes.Ldarg_0); // Serializer
			il.Emit(OpCodes.Ldarg_1); // Stream
			il.Emit(OpCodes.Ldarga_S, 2); // &value
			il.Emit(OpCodes.Call, getValue);

			// XXX for some reason Tailcall causes huge slowdown, at least with "decimal?"
			//il.Emit(OpCodes.Tailcall);
			il.Emit(OpCodes.Call, data.WriterMethodInfo);

			il.MarkLabel(noValueLabel);
			il.Emit(OpCodes.Ret);
		}

		public void GenerateReaderMethod(Serializer serializer, TypeInfo type, ILGenerator il)
		{
			var valueType = type.GenericTypeArguments[0];

			var hasValueLocal = il.DeclareLocal(typeof(bool));
			var valueLocal = il.DeclareLocal(valueType);

			var notNullLabel = il.DefineLabel();

			var data = serializer.GetIndirectData(valueType);

			// read array len
			il.Emit(OpCodes.Ldarg_1); // Stream
			il.Emit(OpCodes.Ldloca_S, hasValueLocal); // &hasValue
			il.Emit(OpCodes.Call, serializer.GetDirectReader(typeof(bool)));

			// if hasValue == 0, return null
			il.Emit(OpCodes.Ldloc_S, hasValueLocal);
			il.Emit(OpCodes.Brtrue_S, notNullLabel);

			il.Emit(OpCodes.Ldarg_2); // &value
			il.Emit(OpCodes.Initobj, type.AsType());
			il.Emit(OpCodes.Ret);

			// hasValue == 1
			il.MarkLabel(notNullLabel);

			if (data.ReaderNeedsInstance)
				il.Emit(OpCodes.Ldarg_0); // Serializer
			il.Emit(OpCodes.Ldarg_1); // Stream
			il.Emit(OpCodes.Ldloca_S, valueLocal);
			il.Emit(OpCodes.Call, data.ReaderMethodInfo);

			il.Emit(OpCodes.Ldarg_2); // &value

			il.Emit(OpCodes.Ldloc_S, valueLocal);
			var constr = type.DeclaredConstructors.First(x => x.GetParameters()[0].ParameterType == valueType);
			il.Emit(OpCodes.Newobj, constr); // new Nullable<T>(valueLocal)

			il.Emit(OpCodes.Stobj, type.AsType()); // store to &value

			il.Emit(OpCodes.Ret);
		}
	}
}
