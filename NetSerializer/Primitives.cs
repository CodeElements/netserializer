/*
 * Copyright 2015 Tomi Valkeinen
 * 
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/.
 */

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NetSerializer
{
	internal static class Primitives
	{
		public static MethodInfo GetWritePrimitive(Type type)
		{
			return typeof(Primitives).GetTypeInfo().DeclaredMethods
				.First(x => x.Name == "WritePrimitive" && x.IsStatic && x.IsPublic &&
				            x.GetParameters().Select(y => y.ParameterType).SequenceEqual(new[] {typeof(Stream), type}));
		}

		public static MethodInfo GetReaderPrimitive(Type type)
		{
			return typeof(Primitives).GetTypeInfo().DeclaredMethods
				.First(x => x.Name == "ReadPrimitive" && x.IsStatic && x.IsPublic &&
				            x.GetParameters().Select(y => y.ParameterType)
					            .SequenceEqual(new[] {typeof(Stream), type.MakeByRefType()}));
		}

		private static uint EncodeZigZag32(int n)
		{
			return (uint) ((n << 1) ^ (n >> 31));
		}

		private static ulong EncodeZigZag64(long n)
		{
			return (ulong) ((n << 1) ^ (n >> 63));
		}

		private static int DecodeZigZag32(uint n)
		{
			return (int) (n >> 1) ^ -(int) (n & 1);
		}

		private static long DecodeZigZag64(ulong n)
		{
			return (long) (n >> 1) ^ -(long) (n & 1);
		}

		private static uint ReadVarint32(Stream stream)
		{
			var result = 0;
			var offset = 0;

			for (; offset < 32; offset += 7)
			{
				var b = stream.ReadByte();
				if (b == -1)
					throw new EndOfStreamException();

				result |= (b & 0x7f) << offset;

				if ((b & 0x80) == 0)
					return (uint) result;
			}

			throw new InvalidDataException();
		}

		private static void WriteVarint32(Stream stream, uint value)
		{
			for (; value >= 0x80u; value >>= 7)
				stream.WriteByte((byte) (value | 0x80u));

			stream.WriteByte((byte) value);
		}

		private static ulong ReadVarint64(Stream stream)
		{
			long result = 0;
			var offset = 0;

			for (; offset < 64; offset += 7)
			{
				var b = stream.ReadByte();
				if (b == -1)
					throw new EndOfStreamException();

				result |= (long) (b & 0x7f) << offset;

				if ((b & 0x80) == 0)
					return (ulong) result;
			}

			throw new InvalidDataException();
		}

		private static void WriteVarint64(Stream stream, ulong value)
		{
			for (; value >= 0x80u; value >>= 7)
				stream.WriteByte((byte) (value | 0x80u));

			stream.WriteByte((byte) value);
		}


		public static void WritePrimitive(Stream stream, bool value)
		{
			stream.WriteByte(value ? (byte) 1 : (byte) 0);
		}

		public static void ReadPrimitive(Stream stream, out bool value)
		{
			var b = stream.ReadByte();
			value = b != 0;
		}

		public static void WritePrimitive(Stream stream, byte value)
		{
			stream.WriteByte(value);
		}

		public static void ReadPrimitive(Stream stream, out byte value)
		{
			value = (byte) stream.ReadByte();
		}

		public static void WritePrimitive(Stream stream, sbyte value)
		{
			stream.WriteByte((byte) value);
		}

		public static void ReadPrimitive(Stream stream, out sbyte value)
		{
			value = (sbyte) stream.ReadByte();
		}

		public static void WritePrimitive(Stream stream, char value)
		{
			WriteVarint32(stream, value);
		}

		public static void ReadPrimitive(Stream stream, out char value)
		{
			value = (char) ReadVarint32(stream);
		}

		public static void WritePrimitive(Stream stream, ushort value)
		{
			WriteVarint32(stream, value);
		}

		public static void ReadPrimitive(Stream stream, out ushort value)
		{
			value = (ushort) ReadVarint32(stream);
		}

		public static void WritePrimitive(Stream stream, short value)
		{
			WriteVarint32(stream, EncodeZigZag32(value));
		}

		public static void ReadPrimitive(Stream stream, out short value)
		{
			value = (short) DecodeZigZag32(ReadVarint32(stream));
		}

		public static void WritePrimitive(Stream stream, uint value)
		{
			WriteVarint32(stream, value);
		}

		public static void ReadPrimitive(Stream stream, out uint value)
		{
			value = ReadVarint32(stream);
		}

		public static void WritePrimitive(Stream stream, int value)
		{
			WriteVarint32(stream, EncodeZigZag32(value));
		}

		public static void ReadPrimitive(Stream stream, out int value)
		{
			value = DecodeZigZag32(ReadVarint32(stream));
		}

		public static void WritePrimitive(Stream stream, ulong value)
		{
			WriteVarint64(stream, value);
		}

		public static void ReadPrimitive(Stream stream, out ulong value)
		{
			value = ReadVarint64(stream);
		}

		public static void WritePrimitive(Stream stream, long value)
		{
			WriteVarint64(stream, EncodeZigZag64(value));
		}

		public static void ReadPrimitive(Stream stream, out long value)
		{
			value = DecodeZigZag64(ReadVarint64(stream));
		}

#if !NO_UNSAFE
		public static unsafe void WritePrimitive(Stream stream, float value)
		{
			var v = *(uint*) &value;
			WriteVarint32(stream, v);
		}

		public static unsafe void ReadPrimitive(Stream stream, out float value)
		{
			var v = ReadVarint32(stream);
			value = *(float*) &v;
		}

		public static unsafe void WritePrimitive(Stream stream, double value)
		{
			var v = *(ulong*) &value;
			WriteVarint64(stream, v);
		}

		public static unsafe void ReadPrimitive(Stream stream, out double value)
		{
			var v = ReadVarint64(stream);
			value = *(double*) &v;
		}
#else
		public static void WritePrimitive(Stream stream, float value)
		{
			WritePrimitive(stream, (double)value);
		}

		public static void ReadPrimitive(Stream stream, out float value)
		{
			double v;
			ReadPrimitive(stream, out v);
			value = (float)v;
		}

		public static void WritePrimitive(Stream stream, double value)
		{
			ulong v = (ulong)BitConverter.DoubleToInt64Bits(value);
			WriteVarint64(stream, v);
		}

		public static void ReadPrimitive(Stream stream, out double value)
		{
			ulong v = ReadVarint64(stream);
			value = BitConverter.Int64BitsToDouble((long)v);
		}
#endif

		public static void WritePrimitive(Stream stream, DateTime value)
		{
			var v = value.ToBinary();
			WritePrimitive(stream, v);
		}

		public static void ReadPrimitive(Stream stream, out DateTime value)
		{
			ReadPrimitive(stream, out long v);
			value = DateTime.FromBinary(v);
		}

		[ThreadStatic] private static int[] _decimalBitsArray;

		public static void WritePrimitive(Stream stream, decimal value)
		{
			var bits = decimal.GetBits(value);

			ulong low = (uint) bits[0];
			var mid = (ulong) (uint) bits[1] << 32;
			var lowmid = low | mid;

			var high = (uint) bits[2];

			var scale = ((uint) bits[3] >> 15) & 0x01fe;
			var sign = (uint) bits[3] >> 31;
			var scaleSign = scale | sign;

			WritePrimitive(stream, lowmid);
			WritePrimitive(stream, high);
			WritePrimitive(stream, scaleSign);
		}

		public static void ReadPrimitive(Stream stream, out decimal value)
		{
			ReadPrimitive(stream, out ulong lowmid);
			ReadPrimitive(stream, out uint high);
			ReadPrimitive(stream, out uint scaleSign);

			var scale = (int) ((scaleSign & ~1) << 15);
			var sign = (int) ((scaleSign & 1) << 31);

			var arr = _decimalBitsArray ?? (_decimalBitsArray = new int[4]);

			arr[0] = (int) lowmid;
			arr[1] = (int) (lowmid >> 32);
			arr[2] = (int) high;
			arr[3] = scale | sign;

			value = new decimal(arr);
		}

		public static void WritePrimitive(Stream stream, string value)
		{
			if (value == null)
			{
				WritePrimitive(stream, (uint) 0);
				return;
			}
			if (value.Length == 0)
			{
				WritePrimitive(stream, (uint) 1);
				return;
			}

			var encoding = new UTF8Encoding(false, true);

			var len = encoding.GetByteCount(value);

			WritePrimitive(stream, (uint) len + 1);
			WritePrimitive(stream, (uint) value.Length);

			var buf = new byte[len];

			encoding.GetBytes(value, 0, value.Length, buf, 0);

			stream.Write(buf, 0, len);
		}

		public static void ReadPrimitive(Stream stream, out string value)
		{
			ReadPrimitive(stream, out uint len);

			if (len == 0)
			{
				value = null;
				return;
			}
			if (len == 1)
			{
				value = string.Empty;
				return;
			}

			ReadPrimitive(stream, out uint _);
			len -= 1;

			var encoding = new UTF8Encoding(false, true);

			var buf = new byte[len];

			var l = 0;

			while (l < len)
			{
				var r = stream.Read(buf, l, (int) len - l);
				if (r == 0)
					throw new EndOfStreamException();
				l += r;
			}

			value = encoding.GetString(buf);
		}


		public static void WritePrimitive(Stream stream, byte[] value)
		{
			if (value == null)
			{
				WritePrimitive(stream, (uint) 0);
				return;
			}

			WritePrimitive(stream, (uint) value.Length + 1);

			stream.Write(value, 0, value.Length);
		}

		private static readonly byte[] EmptyByteArray = new byte[0];

		public static void ReadPrimitive(Stream stream, out byte[] value)
		{
			ReadPrimitive(stream, out uint len);

			if (len == 0)
			{
				value = null;
				return;
			}
			if (len == 1)
			{
				value = EmptyByteArray;
				return;
			}

			len -= 1;

			value = new byte[len];
			var l = 0;

			while (l < len)
			{
				var r = stream.Read(value, l, (int) len - l);
				if (r == 0)
					throw new EndOfStreamException();
				l += r;
			}
		}
	}
}
