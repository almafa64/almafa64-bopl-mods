using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace BoplModSyncer.Utils
{
	public class BaseUtils
	{
		public static string BytesToString(byte[] bytes) =>
			BitConverter.ToString(bytes).Replace("-", "");

		public static string ChecksumFile(string path)
		{
			using SHA256 algorithm = SHA256.Create();
			using FileStream stream = File.OpenRead(path);
			byte[] bytes = algorithm.ComputeHash(stream);
			return BytesToString(bytes) + stream.Length;
		}

		public static string CombineHashes(List<string> hashes)
		{
			StringBuilder sb = new();
			foreach (string hash in hashes)
			{
				sb.Append(hash);
			}

			using SHA256 algorithm = SHA256.Create();
			return BytesToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
		}

		// stolen
		// copies a stream to another asynchronously while reporting progress
		public static async Task CopyToAsync(Stream source, Stream destination, IProgress<int> progress, int bufferSize = 0x1000, CancellationToken cancellationToken = default)
		{
			var buffer = new byte[bufferSize];
			int bytesRead;
			long totalBytesRead = 0;
			while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
			{
				await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
				cancellationToken.ThrowIfCancellationRequested();
				totalBytesRead += bytesRead;
				progress.Report(Convert.ToInt32(totalBytesRead * 100 / source.Length));
			}
		}

		public static Stream GetResourceStream(string namespaceName, string path) =>
			Assembly.GetExecutingAssembly().GetManifestResourceStream($"{namespaceName}.{path}");
	}

	// stolen
	public sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
	{
		private readonly Action<T> _callback = callback;
		void IProgress<T>.Report(T data) => _callback(data);
	}
}