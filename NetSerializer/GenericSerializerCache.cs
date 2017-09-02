using System.IO;
using NetSerializer.Extensions;

namespace NetSerializer
{
	/// <summary>
	///     Generic serializer that uses cached serializers
	/// </summary>
	/// <typeparam name="T">The type to serialize/deserialize</typeparam>
	public static class Serializer<T>
	{
		private static readonly Serializer _serializer;

		static Serializer()
		{
			_serializer = new Serializer(typeof(T).ToEnumerable());
		}

		/// <summary>
		///     Serialize an object into a stream
		/// </summary>
		/// <param name="stream">The stream used to write the object data.</param>
		/// <param name="obj">The <see cref="object" /> to serialize</param>
		public static void Serialize(Stream stream, T obj)
		{
			_serializer.SerializeDirect(stream, obj);
		}

		/// <summary>
		///     Deserialize an object from a stream
		/// </summary>
		/// <param name="stream">The stream that contains the object data to deserialize</param>
		/// <returns>The <see cref="object" /> being deserialized.</returns>
		public static T Deserialize(Stream stream)
		{
			_serializer.DeserializeDirect<T>(stream, out var value);
			return value;
		}
	}
}
