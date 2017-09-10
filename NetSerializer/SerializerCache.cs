using System;
using System.Collections.Concurrent;
using NetSerializer.Extensions;

namespace NetSerializer
{
	/// <summary>
	///     A non generic serializer cache that offers thread-safe access to the serializers
	/// </summary>
	public class SerializerCache
	{
		private readonly ConcurrentDictionary<Type, Serializer> _serializers;

		/// <summary>
		///     Initialize a new instance of <see cref="SerializerCache" />
		/// </summary>
		public SerializerCache()
		{
			_serializers = new ConcurrentDictionary<Type, Serializer>();
		}

		/// <summary>
		///     Get the serialize of a specific type
		/// </summary>
		/// <param name="type">The type that should be worked with</param>
		/// <returns>Return the serializer from the cache that matches the type of create a new one</returns>
		public Serializer GetSerializer(Type type)
		{
			if (_serializers.TryGetValue(type, out var serializer))
				return serializer;

			serializer = new Serializer(type.ToEnumerable());
			_serializers.TryAdd(type, serializer);
			return serializer;
		}
	}
}
