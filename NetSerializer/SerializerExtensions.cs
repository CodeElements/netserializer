using System.IO;

namespace NetSerializer
{
	public static class SerializerExtensions
	{
		public static object Deserialize(this Serializer serializer, byte[] buffer, int index)
		{
			using (var stream = new MemoryStream(buffer, index, buffer.Length - index, false))
			{
				return serializer.Deserialize(stream);
			}
		}

		public static T Deserialize<T>(this Serializer serializer, Stream stream)
		{
			return (T) serializer.Deserialize(stream);
		}

		public static T Deserialize<T>(this Serializer serializer, byte[] buffer, int index)
		{
			using (var stream = new MemoryStream(buffer, index, buffer.Length - index, false))
			{
				return (T) serializer.Deserialize(stream);
			}
		}
	}
}
