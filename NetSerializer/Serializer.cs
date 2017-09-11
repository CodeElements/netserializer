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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using NetSerializer.Extensions;
using NetSerializer.TypeSerializers;

namespace NetSerializer
{
	internal delegate void SerializeDelegate<in T>(Serializer serializer, Stream stream, T ob);

	internal delegate void DeserializeDelegate<T>(Serializer serializer, Stream stream, out T ob);

	/// <summary>
	///     NetSerializer is a simple and very fast serializer for .Net languages.
	/// </summary>
	public class Serializer
	{
		private static readonly ITypeSerializer[] TypeSerializers =
		{
			new ObjectSerializer(),
			new PrimitivesSerializer(),
			new ArraySerializer(),
			new EnumSerializer(),
			new DictionarySerializer(),
			new NullableSerializer(),
			new GenericSerializer()
		};

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="rootType">The type to be de(serialized)</param>
		public Serializer(Type rootType) : this(rootType.ToEnumerable(), new Settings())
		{
		}

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="rootTypes">The types to be de(serialized)</param>
		public Serializer(params Type[] rootTypes) : this(rootTypes, new Settings())
		{
		}

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="rootTypes">Types to be (de)serialized</param>
		public Serializer(IEnumerable<Type> rootTypes)
			: this(rootTypes, new Settings())
		{
		}

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="rootTypes">Types to be (de)serialized</param>
		/// <param name="settings">Settings</param>
		public Serializer(IEnumerable<Type> rootTypes, Settings settings)
		{
			Settings = settings;

			if (Settings.CustomTypeSerializers.All(s => s is IDynamicTypeSerializer || s is IStaticTypeSerializer) == false)
				throw new ArgumentException("TypeSerializers have to implement IDynamicTypeSerializer or IStaticTypeSerializer");

			lock (_modifyLock)
			{
				_runtimeTypeMap = new TypeDictionary();
				_runtimeTypeIdList = new TypeIdList();

				AddTypesInternal(new Dictionary<Type, uint>
				{
					{typeof(object), ObjectTypeId}
				});

				AddTypesInternal(rootTypes);

				GenerateWriters(typeof(object).GetTypeInfo());
				GenerateReaders(typeof(object).GetTypeInfo());
			}
		}

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="typeMap">Type -> typeID map</param>
		public Serializer(Dictionary<Type, uint> typeMap)
			: this(typeMap, new Settings())
		{
		}

		/// <summary>
		///     Initialize NetSerializer
		/// </summary>
		/// <param name="typeMap">Type -> typeID map</param>
		/// <param name="settings">Settings</param>
		public Serializer(Dictionary<Type, uint> typeMap, Settings settings)
		{
			Settings = settings;

			if (Settings.CustomTypeSerializers.All(s => s is IDynamicTypeSerializer || s is IStaticTypeSerializer) == false)
				throw new ArgumentException("TypeSerializers have to implement IDynamicTypeSerializer or IStaticTypeSerializer");

			lock (_modifyLock)
			{
				_runtimeTypeMap = new TypeDictionary();
				_runtimeTypeIdList = new TypeIdList();

				AddTypesInternal(new Dictionary<Type, uint>
				{
					{typeof(object), ObjectTypeId}
				});

				AddTypesInternal(typeMap);

				GenerateWriters(typeof(object).GetTypeInfo());
				GenerateReaders(typeof(object).GetTypeInfo());
			}
		}

		private Dictionary<Type, uint> AddTypesInternal(IEnumerable<Type> roots)
		{
			AssertLocked();

			var stack = new Stack<Type>(roots);
			var addedMap = new Dictionary<Type, uint>();

			while (stack.Count > 0)
			{
				var type = stack.Pop();
				var typeInfo = type.GetTypeInfo();

				if (_runtimeTypeMap.ContainsKey(type))
					continue;

				if (typeInfo.IsAbstract || typeInfo.IsInterface)
					continue;

				if (typeInfo.ContainsGenericParameters)
					throw new NotSupportedException(string.Format("Type {0} contains generic parameters", type.FullName));

				while (_runtimeTypeIdList.ContainsTypeId(_nextAvailableTypeId))
					_nextAvailableTypeId++;

				var typeId = _nextAvailableTypeId++;

				var serializer = GetTypeSerializer(typeInfo);

				var data = new TypeData(typeInfo, type, typeId, serializer);
				_runtimeTypeMap[type] = data;
				_runtimeTypeIdList[typeId] = data;

				addedMap[type] = typeId;

				foreach (var t in serializer.GetSubtypes(typeInfo))
					if (_runtimeTypeMap.ContainsKey(t) == false)
						stack.Push(t);
			}

			return addedMap;
		}

		private void AddTypesInternal(Dictionary<Type, uint> typeMap)
		{
			AssertLocked();

			foreach (var kvp in typeMap)
			{
				var type = kvp.Key;
				var typeInfo = type.GetTypeInfo();
				var typeId = kvp.Value;

				if (type == null)
					throw new ArgumentException("Null type in dictionary");

				if (typeId == 0)
					throw new ArgumentException("TypeID 0 is reserved");

				if (_runtimeTypeMap.ContainsKey(type))
				{
					if (_runtimeTypeMap[type].TypeId != typeId)
						throw new ArgumentException(string.Format("Type {0} already added with different TypeID", type.FullName));

					continue;
				}

				if (_runtimeTypeIdList.ContainsTypeId(typeId))
					throw new ArgumentException(string.Format("Type with typeID {0} already added", typeId));

				if (typeInfo.IsAbstract || typeInfo.IsInterface)
					throw new ArgumentException(string.Format("Type {0} is abstract or interface", type.FullName));

				if (typeInfo.ContainsGenericParameters)
					throw new NotSupportedException(string.Format("Type {0} contains generic parameters", type.FullName));

				var serializer = GetTypeSerializer(typeInfo);

				var data = new TypeData(typeInfo, type, typeId, serializer);
				_runtimeTypeMap[type] = data;
				_runtimeTypeIdList[typeId] = data;
			}
		}

		/// <summary>
		///     Get a Dictionary&lt;&gt; containing a mapping of all the serializer's Types to TypeIDs
		/// </summary>
		public Dictionary<Type, uint> GetTypeMap()
		{
			lock (_modifyLock)
			{
				return _runtimeTypeMap.ToDictionary();
			}
		}

		/// <summary>
		///     Add rootTypes and all their subtypes, and return a mapping of all added types to typeIDs
		/// </summary>
		public Dictionary<Type, uint> AddTypes(IEnumerable<Type> rootTypes)
		{
			lock (_modifyLock)
			{
				return AddTypesInternal(rootTypes);
			}
		}

		/// <summary>
		///     Add types obtained by a call to AddTypes in another Serializer instance
		/// </summary>
		public void AddTypes(Dictionary<Type, uint> typeMap)
		{
			lock (_modifyLock)
			{
				AddTypesInternal(typeMap);
			}
		}

		/// <summary>
		///     Get SHA256 of the serializer type data. The SHA includes TypeIDs and Type's full names.
		///     The SHA can be used as a relatively good check to verify that two serializers
		///     (e.g. client and server) have the same type data.
		/// </summary>
		public string GetSha256()
		{
			using (var stream = new MemoryStream())
			using (var writer = new StreamWriter(stream))
			{
				lock (_modifyLock)
				{
					foreach (var item in _runtimeTypeIdList.ToSortedList())
					{
						writer.Write(item.Key);
						writer.Write(item.Value.FullName);
					}
				}

				var sha256 = SHA256.Create();
				var bytes = sha256.ComputeHash(stream);

				var sb = new StringBuilder();
				foreach (var b in bytes)
					sb.Append(b.ToString("x2"));
				return sb.ToString();
			}
		}

		private readonly TypeDictionary _runtimeTypeMap;
		private readonly TypeIdList _runtimeTypeIdList;

		private readonly object _modifyLock = new object();

		private uint _nextAvailableTypeId = 1;

		internal const uint ObjectTypeId = 1;

		internal readonly Settings Settings;

		[Conditional("DEBUG")]
		private void AssertLocked()
		{
			Debug.Assert(Monitor.IsEntered(_modifyLock));
		}

		public void Serialize(Stream stream, object ob)
		{
			ObjectSerializer.Serialize(this, stream, ob);
		}

		public object Deserialize(Stream stream)
		{
			ObjectSerializer.Deserialize(this, stream, out object ob);
			return ob;
		}

		public void Deserialize(Stream stream, out object ob)
		{
			ObjectSerializer.Deserialize(this, stream, out ob);
		}

		/// <summary>
		///     Serialize object graph without writing the type-id of the root type. This can be useful e.g. when
		///     serializing a known value type, as this will avoid boxing.
		/// </summary>
		public void SerializeDirect<T>(Stream stream, T value)
		{
			var del = (SerializeDelegate<T>) _runtimeTypeMap[typeof(T)].WriterDirectDelegate;

			if (del == null)
				lock (_modifyLock)
				{
					del = (SerializeDelegate<T>) GenerateDirectWriterDelegate(typeof(T).GetTypeInfo());
				}

			del(this, stream, value);
		}

		/// <summary>
		///     Deserialize object graph serialized with SerializeDirect(). Type T must match the type used when
		///     serializing.
		/// </summary>
		public void DeserializeDirect<T>(Stream stream, out T value)
		{
			var del = (DeserializeDelegate<T>) _runtimeTypeMap[typeof(T)].ReaderDirectDelegate;

			if (del == null)
				lock (_modifyLock)
				{
					del = (DeserializeDelegate<T>) GenerateDirectReaderDelegate(typeof(T).GetTypeInfo());
				}

			del(this, stream, out value);
		}

		internal uint GetTypeIdAndSerializer(TypeInfo type, out SerializeDelegate<object> del)
		{
			var data = _runtimeTypeMap[type.AsType()];

			if (data.WriterTrampolineDelegate != null)
			{
				del = data.WriterTrampolineDelegate;
				return data.TypeId;
			}

			lock (_modifyLock)
			{
				del = GenerateWriterTrampoline(type);
				return data.TypeId;
			}
		}

		internal DeserializeDelegate<object> GetDeserializeTrampolineFromId(uint id)
		{
			var data = _runtimeTypeIdList[id];

			if (data.ReaderTrampolineDelegate != null)
				return data.ReaderTrampolineDelegate;

			lock (_modifyLock)
			{
				return GenerateReaderTrampoline(data.TypeInfo);
			}
		}

		private ITypeSerializer GetTypeSerializer(TypeInfo type)
		{
			var serializer = Settings.CustomTypeSerializers.FirstOrDefault(h => h.Handles(type)) ??
			                 TypeSerializers.FirstOrDefault(h => h.Handles(type));
			if (serializer == null)
				throw new NotSupportedException(string.Format("No serializer for {0}", type.FullName));

			return serializer;
		}

		internal TypeData GetIndirectData(Type type)
		{
			if (!_runtimeTypeMap.TryGetValue(type, out TypeData data) || data.CanCallDirect == false)
				return _runtimeTypeMap[typeof(object)];

			return data;
		}

		internal MethodInfo GetDirectWriter(Type type)
		{
			return _runtimeTypeMap[type].WriterMethodInfo;
		}

		internal MethodInfo GetDirectReader(Type type)
		{
			return _runtimeTypeMap[type].ReaderMethodInfo;
		}


		private HashSet<TypeInfo> Collect(TypeInfo rootType)
		{
			var l = new HashSet<TypeInfo>();
			var stack = new Stack<TypeInfo>();

			stack.Push(rootType);

			while (stack.Count > 0)
			{
				var type = stack.Pop();

				if (type.IsAbstract || type.IsInterface)
					continue;

				if (type.ContainsGenericParameters)
					throw new NotSupportedException(string.Format("Type {0} contains generic parameters", type.FullName));

				var serializer = _runtimeTypeMap[type.AsType()].TypeSerializer;

				foreach (var t in serializer.GetSubtypes(type))
				{
					var ti = t.GetTypeInfo();
					if (l.Contains(ti) == false)
						stack.Push(ti);
				}

				l.Add(type);
			}

			return l;
		}

		private void GenerateWriterStub(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			var serializer = data.TypeSerializer;

			MethodInfo writer;
			if (serializer is IStaticTypeSerializer typeSerializer)
			{
				var sts = typeSerializer;
				writer = sts.GetStaticWriter(type);

				Debug.Assert(writer != null);
			}
			else if (serializer is IDynamicTypeSerializer)
			{
				// TODO: make it possible for dyn serializers to not have Serializer param
				writer = Helpers.GenerateDynamicSerializerStub(type.AsType());
			}
			else
			{
				throw new Exception();
			}

			data.WriterMethodInfo = writer;
		}

		private void GenerateWriterBody(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			var serializer = data.TypeSerializer;

			var writer = data.WriterMethodInfo as DynamicMethod;
			if (writer == null)
				return;

			var dynSer = (IDynamicTypeSerializer) serializer;

			dynSer.GenerateWriterMethod(this, type, writer.GetILGenerator());
		}

		private void GenerateWriters(TypeInfo rootType)
		{
			AssertLocked();

			if (_runtimeTypeMap[rootType.AsType()].WriterMethodInfo != null)
				return;

			var types = Collect(rootType).Where(t => _runtimeTypeMap[t.AsType()].WriterMethodInfo == null).ToList();

			foreach (var type in types)
				GenerateWriterStub(type);

			foreach (var type in types)
				GenerateWriterBody(type);
		}

		private SerializeDelegate<object> GenerateWriterTrampoline(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			if (data.WriterTrampolineDelegate != null)
				return data.WriterTrampolineDelegate;

			GenerateWriters(type);

			data.WriterTrampolineDelegate = (SerializeDelegate<object>) Helpers.CreateSerializeDelegate(typeof(object), data);
			return data.WriterTrampolineDelegate;
		}

		private Delegate GenerateDirectWriterDelegate(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			if (data.WriterDirectDelegate != null)
				return data.WriterDirectDelegate;

			GenerateWriters(type);

			data.WriterDirectDelegate = Helpers.CreateSerializeDelegate(type.AsType(), data);
			return data.WriterDirectDelegate;
		}

		private void GenerateReaderStub(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			var serializer = data.TypeSerializer;

			MethodInfo reader;

			if (serializer is IStaticTypeSerializer sts)
			{
				reader = sts.GetStaticReader(type);
				Debug.Assert(reader != null);
			}
			else if (serializer is IDynamicTypeSerializer)
			{
				// TODO: make it possible for dyn serializers to not have Serializer param
				reader = Helpers.GenerateDynamicDeserializerStub(type.AsType());
			}
			else
			{
				throw new Exception();
			}

			data.ReaderMethodInfo = reader;
		}

		private void GenerateReaderBody(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			var serializer = data.TypeSerializer;

			var reader = data.ReaderMethodInfo as DynamicMethod;
			if (reader == null)
				return;

			var dynSer = (IDynamicTypeSerializer) serializer;

			dynSer.GenerateReaderMethod(this, type, reader.GetILGenerator());
		}

		private void GenerateReaders(TypeInfo rootType)
		{
			AssertLocked();

			if (_runtimeTypeMap[rootType.AsType()].ReaderMethodInfo != null)
				return;

			var types = Collect(rootType).Where(t => _runtimeTypeMap[t.AsType()].ReaderMethodInfo == null).ToList();

			foreach (var type in types)
				GenerateReaderStub(type);

			foreach (var type in types)
				GenerateReaderBody(type);
		}

		private DeserializeDelegate<object> GenerateReaderTrampoline(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			if (data.ReaderTrampolineDelegate != null)
				return data.ReaderTrampolineDelegate;

			GenerateReaders(type);

			data.ReaderTrampolineDelegate =
				(DeserializeDelegate<object>) Helpers.CreateDeserializeDelegate(typeof(object), data);
			return data.ReaderTrampolineDelegate;
		}

		private Delegate GenerateDirectReaderDelegate(TypeInfo type)
		{
			AssertLocked();

			var data = _runtimeTypeMap[type.AsType()];

			if (data.ReaderDirectDelegate != null)
				return data.ReaderDirectDelegate;

			GenerateReaders(type);

			data.ReaderDirectDelegate = Helpers.CreateDeserializeDelegate(type.AsType(), data);
			return data.ReaderDirectDelegate;
		}


#if GENERATE_DEBUGGING_ASSEMBLY

		public static void GenerateDebugAssembly(IEnumerable<Type> rootTypes, Settings settings)
		{
			new Serializer(rootTypes, settings, true);
		}

		Serializer(IEnumerable<Type> rootTypes, Settings settings, bool debugAssembly)
		{
			this.Settings = settings;

			if (this.Settings.CustomTypeSerializers.All(s => s is IDynamicTypeSerializer || s is IStaticTypeSerializer) == false)
				throw new ArgumentException("TypeSerializers have to implement IDynamicTypeSerializer or  IStaticTypeSerializer");

			var ab =
AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("NetSerializerDebug"), AssemblyBuilderAccess.RunAndSave);
			var modb = ab.DefineDynamicModule("NetSerializerDebug.dll");
			var tb = modb.DefineType("NetSerializer", TypeAttributes.Public);

			_runtimeTypeMap = new TypeDictionary();
			_runtimeTypeIdList = new TypeIDList();

			lock (_modifyLock)
			{
				var addedTypes = AddTypesInternal(new[] { typeof(object) }.Concat(rootTypes));

				/* generate stubs */
				foreach (var type in addedTypes.Keys)
					GenerateDebugStubs(type, tb);

				foreach (var type in addedTypes.Keys)
					GenerateDebugBodies(type);
			}

			tb.CreateType();
			ab.Save("NetSerializerDebug.dll");
		}

		void GenerateDebugStubs(Type type, TypeBuilder tb)
		{
			var data = _runtimeTypeMap[type];

			ITypeSerializer serializer = data.TypeSerializer;

			MethodInfo writer;
			MethodInfo reader;
			bool writerNeedsInstance, readerNeedsInstance;

			if (serializer is IStaticTypeSerializer)
			{
				var sts = (IStaticTypeSerializer)serializer;

				writer = sts.GetStaticWriter(type);
				reader = sts.GetStaticReader(type);

				writerNeedsInstance = writer.GetParameters().Length == 3;
				readerNeedsInstance = reader.GetParameters().Length == 3;
			}
			else if (serializer is IDynamicTypeSerializer)
			{
				writer = Helpers.GenerateStaticSerializerStub(tb, type);
				reader = Helpers.GenerateStaticDeserializerStub(tb, type);

				writerNeedsInstance = readerNeedsInstance = true;
			}
			else
			{
				throw new Exception();
			}

			data.WriterMethodInfo = writer;
			data.WriterNeedsInstanceDebug = writerNeedsInstance;

			data.ReaderMethodInfo = reader;
			data.ReaderNeedsInstanceDebug = readerNeedsInstance;
		}

		void GenerateDebugBodies(Type type)
		{
			var data = _runtimeTypeMap[type];

			ITypeSerializer serializer = data.TypeSerializer;

			var dynSer = serializer as IDynamicTypeSerializer;
			if (dynSer == null)
				return;

			var writer = data.WriterMethodInfo as MethodBuilder;
			if (writer == null)
				throw new Exception();

			var reader = data.ReaderMethodInfo as MethodBuilder;
			if (reader == null)
				throw new Exception();

			dynSer.GenerateWriterMethod(this, type, writer.GetILGenerator());
			dynSer.GenerateReaderMethod(this, type, reader.GetILGenerator());
		}
#endif
	}
}
