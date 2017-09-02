using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using NetSerializer;

namespace PrimitiveTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var stream = new MemoryStream();
			var testClass = new TestClass{ASd = 123, Test = "Hello"};
			Serializer<TestClass>.Serialize(stream, testClass);

			stream.Position = 0;
			var result = Serializer<TestClass>.Deserialize(stream);
		}
	}

	[Serializable]
	public class TestClass
	{
		public int ASd { get; set; }
		public string Test { get; set; }
	}
}
