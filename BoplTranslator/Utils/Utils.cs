using System;
using System.Linq;

namespace BoplTranslator
{
	public static class Utils
	{
		public static int MaxOfEnum<T>() => Enum.GetValues(typeof(T)).Cast<int>().Max();
		public static int MinOfEnum<T>() => Enum.GetValues(typeof(T)).Cast<int>().Min();
	}
}
