using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace BoplTranslator
{
	public static class Utils
	{
		public static MethodInfo GetMethod(string MethodName, BindingFlags bindingAttributes = BindingFlags.NonPublic | BindingFlags.Static)
		{
			StackTrace stackTrace = new StackTrace();
			Type callingType = stackTrace.GetFrame(1).GetMethod().DeclaringType;
			return callingType.GetMethod(MethodName, bindingAttributes);
		}

		public static int MaxOfEnum<T>() => Enum.GetValues(typeof(T)).Cast<int>().Max();
		public static int MinOfEnum<T>() => Enum.GetValues(typeof(T)).Cast<int>().Min();
	}
}
