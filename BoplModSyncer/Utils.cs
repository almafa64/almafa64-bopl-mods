using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BoplModSyncer
{
	public class Utils
	{
		public static string ChecksumFile(string path)
		{
			using SHA256 algorithm = SHA256.Create();
			using FileStream stream = File.OpenRead(path);
			byte[] bytes = algorithm.ComputeHash(stream);
			string tmp = BitConverter.ToString(bytes).Replace("-", "") + stream.Length;
			return tmp;
		}

		public static string CombineHashes(List<string> hashes)
		{
			StringBuilder sb = new();
			foreach (string hash in hashes)
			{
				sb.Append(hash);
			}
			using(SHA256 hash = SHA256.Create())
			{
				byte[] bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
				return BitConverter.ToString(bytes).Replace("-", "");
			}
		}
	}
}