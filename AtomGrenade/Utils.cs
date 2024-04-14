using System.IO;
using System.Reflection;
using UnityEngine;

namespace AtomGrenade
{
	internal class Utils
	{
		public static Texture2D LoadDLLTexture(string path)
		{
			using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
			byte[] buffer = new byte[stream.Length];
			stream.Read(buffer, 0, buffer.Length);
			Texture2D texture = new(256, 256);
			texture.LoadImage(buffer);
			return texture;
		}
	}
}
