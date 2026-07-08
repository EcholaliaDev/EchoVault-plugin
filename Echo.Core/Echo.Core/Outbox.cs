using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Echo.Core;

public sealed class Outbox
{
	private readonly object _lock = new object();

	private readonly string _path;

	private readonly long _maxBytes;

	public Outbox(string path, long maxBytes = 10485760L)
	{
		_path = path;
		_maxBytes = maxBytes;
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
		TruncateTornTail();
	}

	public void Append(string jsonLine)
	{
		lock (_lock)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(jsonLine + "\n");
			if (bytes.Length > _maxBytes)
			{
				return;
			}
			long current = (File.Exists(_path) ? new FileInfo(_path).Length : 0);
			if (current + bytes.Length > _maxBytes)
			{
				EvictOldest(current + bytes.Length - _maxBytes);
			}
			using FileStream stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
			stream.Write(bytes);
			stream.Flush(flushToDisk: true);
		}
	}

	public IReadOnlyList<string> Peek(int max)
	{
		lock (_lock)
		{
			return ReadLines().Take(max).ToList();
		}
	}

	public void Commit(int count)
	{
		lock (_lock)
		{
			RewriteWithout(count);
		}
	}

	public int Count()
	{
		lock (_lock)
		{
			return ReadLines().Count();
		}
	}

	public long SizeBytes()
	{
		lock (_lock)
		{
			return File.Exists(_path) ? new FileInfo(_path).Length : 0;
		}
	}

	private IEnumerable<string> ReadLines()
	{
		if (!File.Exists(_path))
		{
			yield break;
		}
		using StreamReader reader = new StreamReader(new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
		while (true)
		{
			string line = reader.ReadLine();
			if (line != null)
			{
				if (line.Length > 0)
				{
					yield return line;
				}
				continue;
			}
			break;
		}
	}

	private void TruncateTornTail()
	{
		if (!File.Exists(_path))
		{
			return;
		}
		using FileStream stream = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
		if (stream.Length != 0L)
		{
			stream.Seek(-1L, SeekOrigin.End);
			if (stream.ReadByte() != 10)
			{
				byte[] buffer = new byte[stream.Length];
				stream.Position = 0L;
				stream.ReadExactly(buffer);
				int lastNewline = Array.LastIndexOf(buffer, (byte)10);
				stream.SetLength(lastNewline + 1);
			}
		}
	}

	private void EvictOldest(long bytesToFree)
	{
		List<string> lines = ReadLines().ToList();
		long freed = 0L;
		int skip = 0;
		foreach (string line in lines)
		{
			if (freed >= bytesToFree)
			{
				break;
			}
			freed += Encoding.UTF8.GetByteCount(line) + 1;
			skip++;
		}
		WriteAll(lines.Skip(skip));
	}

	private void RewriteWithout(int count)
	{
		WriteAll(ReadLines().Skip(count).ToList());
	}

	private void WriteAll(IEnumerable<string> lines)
	{
		string temp = _path + ".tmp";
		using (FileStream stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			foreach (string line in lines)
			{
				stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
			}
			stream.Flush(flushToDisk: true);
		}
		File.Move(temp, _path, overwrite: true);
	}
}
