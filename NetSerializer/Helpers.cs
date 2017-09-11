﻿/*
 * Copyright 2015 Tomi Valkeinen
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NetSerializer
{
	internal static class Helpers
	{
		public static IEnumerable<FieldInfo> GetFieldInfos(TypeInfo type)
		{
			Debug.Assert(type.IsSerializable);

			var fields = type.DeclaredFields
				.Where(fi => (fi.Attributes & FieldAttributes.NotSerialized) == 0 && !fi.IsStatic && !fi.IsLiteral)
				.OrderBy(f => f.Name, StringComparer.Ordinal);

			if (type.BaseType == null)
				return fields;
			var baseFields = GetFieldInfos(type.BaseType.GetTypeInfo());
			return baseFields.Concat(fields);
		}

		public static DynamicMethod GenerateDynamicSerializerStub(Type type)
		{
			var dm = new DynamicMethod("Serialize", null,
				new[] {typeof(Serializer), typeof(Stream), type},
				typeof(Serializer), true);

			return dm;
		}

		public static DynamicMethod GenerateDynamicDeserializerStub(Type type)
		{
			var dm = new DynamicMethod("Deserialize", null,
				new[] {typeof(Serializer), typeof(Stream), type.MakeByRefType()},
				typeof(Serializer), true);

			return dm;
		}

#if GENERATE_DEBUGGING_ASSEMBLY
		public static MethodBuilder GenerateStaticSerializerStub(TypeBuilder tb, Type type)
		{
			var mb = tb.DefineMethod("Serialize", MethodAttributes.Public | MethodAttributes.Static, null,
				new Type[] { typeof(Serializer), typeof(Stream), type });
			mb.DefineParameter(1, ParameterAttributes.None, "serializer");
			mb.DefineParameter(2, ParameterAttributes.None, "stream");
			mb.DefineParameter(3, ParameterAttributes.None, "value");
			return mb;
		}

		public static MethodBuilder GenerateStaticDeserializerStub(TypeBuilder tb, Type type)
		{
			var mb = tb.DefineMethod("Deserialize", MethodAttributes.Public | MethodAttributes.Static, null,
				new Type[] { typeof(Serializer), typeof(Stream), type.MakeByRefType() });
			mb.DefineParameter(1, ParameterAttributes.None, "serializer");
			mb.DefineParameter(2, ParameterAttributes.None, "stream");
			mb.DefineParameter(3, ParameterAttributes.Out, "value");
			return mb;
		}
#endif

		/// <summary>
		///     Create delegate that calls writer either directly, or via a trampoline
		/// </summary>
		public static Delegate CreateSerializeDelegate(Type paramType, TypeData data)
		{
			var writerType = data.Type;
			var writerTypeInfo = data.TypeInfo;

			if (paramType != writerType && paramType != typeof(object))
				throw new Exception();

			var needTypeConv = paramType != writerType;
			var needsInstanceParameter = data.WriterNeedsInstance;

			var delegateType = typeof(SerializeDelegate<>).MakeGenericType(paramType);

			// Can we call the writer directly?

			if (!needTypeConv && needsInstanceParameter)
			{
				var dynamicWriter = data.WriterMethodInfo as DynamicMethod;

				if (dynamicWriter != null)
					return dynamicWriter.CreateDelegate(delegateType);
				return data.WriterMethodInfo.CreateDelegate(delegateType);
			}

			// Create a trampoline

			var wrapper = GenerateDynamicSerializerStub(paramType);
			var il = wrapper.GetILGenerator();

			if (needsInstanceParameter)
				il.Emit(OpCodes.Ldarg_0);

			il.Emit(OpCodes.Ldarg_1);
			il.Emit(OpCodes.Ldarg_2);
			if (needTypeConv)
				il.Emit(writerTypeInfo.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, writerType);

			// XXX tailcall causes slowdowns with large valuetypes
			//il.Emit(OpCodes.Tailcall);
			il.Emit(OpCodes.Call, data.WriterMethodInfo);

			il.Emit(OpCodes.Ret);

			return wrapper.CreateDelegate(delegateType);
		}

		/// <summary>
		///     Create delegate that calls reader either directly, or via a trampoline
		/// </summary>
		public static Delegate CreateDeserializeDelegate(Type paramType, TypeData data)
		{
			var readerType = data.Type;
			var readerTypeInfo = data.TypeInfo;

			if (paramType != readerType && paramType != typeof(object))
				throw new Exception();

			var needTypeConv = paramType != readerType;
			var needsInstanceParameter = data.ReaderNeedsInstance;

			var delegateType = typeof(DeserializeDelegate<>).MakeGenericType(paramType);

			// Can we call the reader directly?

			if (!needTypeConv && needsInstanceParameter)
			{
				var dynamicReader = data.ReaderMethodInfo as DynamicMethod;

				if (dynamicReader != null)
					return dynamicReader.CreateDelegate(delegateType);
				return data.ReaderMethodInfo.CreateDelegate(delegateType);
			}

			// Create a trampoline

			var wrapper = GenerateDynamicDeserializerStub(paramType);
			var il = wrapper.GetILGenerator();

			if (needsInstanceParameter)
				il.Emit(OpCodes.Ldarg_0);

			if (needTypeConv && readerTypeInfo.IsValueType)
			{
				var local = il.DeclareLocal(readerType);

				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldloca_S, local);

				il.Emit(OpCodes.Call, data.ReaderMethodInfo);

				// write result object to out object
				il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldloc_0);
				il.Emit(OpCodes.Box, readerType);
				il.Emit(OpCodes.Stind_Ref);
			}
			else
			{
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldarg_2);

				// XXX tailcall causes slowdowns with large valuetypes
				//il.Emit(OpCodes.Tailcall);
				il.Emit(OpCodes.Call, data.ReaderMethodInfo);
			}

			il.Emit(OpCodes.Ret);

			return wrapper.CreateDelegate(delegateType);
		}
	}
}
