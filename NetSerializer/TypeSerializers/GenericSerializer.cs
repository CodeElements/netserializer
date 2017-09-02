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
using System.Runtime.Serialization;
using NetSerializer.Extensions;

namespace NetSerializer.TypeSerializers
{
	internal sealed class GenericSerializer : IDynamicTypeSerializer
	{
		public bool Handles(TypeInfo type)
		{
			if (!type.IsSerializable)
				throw new NotSupportedException(string.Format("Type {0} is not marked as Serializable", type.FullName));

			return true;
		}

		public IEnumerable<Type> GetSubtypes(TypeInfo type)
		{
			var fields = Helpers.GetFieldInfos(type);

			foreach (var field in fields)
				yield return field.FieldType;
		}

		public void GenerateWriterMethod(Serializer serializer, TypeInfo type, ILGenerator il)
		{
			// arg0: Serializer, arg1: Stream, arg2: value

			if (serializer.Settings.SupportSerializationCallbacks)
				foreach (var m in GetMethodsWithAttributes<OnSerializingAttribute>(type))
					EmitCallToSerializingCallback(type, il, m);

			var fields = Helpers.GetFieldInfos(type);

			foreach (var field in fields)
			{
				// Note: the user defined value type is not passed as reference. could cause perf problems with big structs

				var fieldType = field.FieldType;

				var data = serializer.GetIndirectData(fieldType);

				if (data.WriterNeedsInstance)
					il.Emit(OpCodes.Ldarg_0);

				il.Emit(OpCodes.Ldarg_1);
				if (type.IsValueType)
					il.Emit(OpCodes.Ldarga_S, 2);
				else
					il.Emit(OpCodes.Ldarg_2);
				il.Emit(OpCodes.Ldfld, field);

				il.Emit(OpCodes.Call, data.WriterMethodInfo);
			}

			if (serializer.Settings.SupportSerializationCallbacks)
				foreach (var m in GetMethodsWithAttributes<OnSerializedAttribute>(type))
					EmitCallToSerializingCallback(type, il, m);

			il.Emit(OpCodes.Ret);
		}

		public void GenerateReaderMethod(Serializer serializer, TypeInfo type, ILGenerator il)
		{
			// arg0: Serializer, arg1: stream, arg2: out value

			if (type.IsClass)
			{
				// instantiate empty class
				il.Emit(OpCodes.Ldarg_2);


				//var gtfh = typeof(Type).GetTypeInfo().GetDeclaredMethod("GetTypeFromHandle");
				//il.Emit(OpCodes.Ldtoken, type.AsType());
				//il.Emit(OpCodes.Call, gtfh);
				il.Emit(OpCodes.Newobj, type.DeclaredConstructors.First(x => x.GetParameters().Length == 0));

				/*var guo = typeof(System.Runtime.Serialization.FormatterServices).GetMethod("GetUninitializedObject", BindingFlags.Public | BindingFlags.Static);
				il.Emit(OpCodes.Ldtoken, type.AsType());
				il.Emit(OpCodes.Call, gtfh);
				il.Emit(OpCodes.Call, guo);
				il.Emit(OpCodes.Castclass, type);*/

				il.Emit(OpCodes.Stind_Ref);
			}

			if (serializer.Settings.SupportSerializationCallbacks)
				foreach (var m in GetMethodsWithAttributes<OnDeserializingAttribute>(type))
					EmitCallToDeserializingCallback(type, il, m);

			var fields = Helpers.GetFieldInfos(type);

			foreach (var field in fields)
			{
				var fieldType = field.FieldType;

				var data = serializer.GetIndirectData(fieldType);

				if (data.ReaderNeedsInstance)
					il.Emit(OpCodes.Ldarg_0);

				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Ldarg_2);
				if (type.IsClass)
					il.Emit(OpCodes.Ldind_Ref);
				il.Emit(OpCodes.Ldflda, field);

				il.Emit(OpCodes.Call, data.ReaderMethodInfo);
			}

			if (serializer.Settings.SupportSerializationCallbacks)
				foreach (var m in GetMethodsWithAttributes<OnDeserializedAttribute>(type))
					EmitCallToDeserializingCallback(type, il, m);

			il.Emit(OpCodes.Ret);
		}

		private static IEnumerable<MethodInfo> GetMethodsWithAttributes<TAttribute>(TypeInfo type)
			where TAttribute : Attribute
		{
			var methods = type.GetAllMethods().Where(x => x.GetCustomAttribute<TAttribute>() != null);

			if (type.BaseType == null)
			{
				return methods;
			}
			var baseMethods = GetMethodsWithAttributes<TAttribute>(type.BaseType.GetTypeInfo());
			return baseMethods.Concat(methods);
		}

		private static void EmitCallToSerializingCallback(TypeInfo type, ILGenerator il, MethodInfo method)
		{
			if (type.IsValueType)
				throw new NotImplementedException("Serialization callbacks not supported for Value types");

			if (type.IsValueType)
				il.Emit(OpCodes.Ldarga_S, 2);
			else
				il.Emit(OpCodes.Ldarg_2);

			var ctxLocal = il.DeclareLocal(typeof(StreamingContext));
			il.Emit(OpCodes.Ldloca_S, ctxLocal);
			il.Emit(OpCodes.Initobj, typeof(StreamingContext));
			il.Emit(OpCodes.Ldloc_S, ctxLocal);

			il.Emit(OpCodes.Call, method);
		}

		private static void EmitCallToDeserializingCallback(TypeInfo type, ILGenerator il, MethodInfo method)
		{
			if (type.IsValueType)
				throw new NotImplementedException("Serialization callbacks not supported for Value types");

			il.Emit(OpCodes.Ldarg_2);
			if (type.IsClass)
				il.Emit(OpCodes.Ldind_Ref);

			var ctxLocal = il.DeclareLocal(typeof(StreamingContext));
			il.Emit(OpCodes.Ldloca_S, ctxLocal);
			il.Emit(OpCodes.Initobj, typeof(StreamingContext));
			il.Emit(OpCodes.Ldloc_S, ctxLocal);

			il.Emit(OpCodes.Call, method);
		}
	}
}
