using System.IO;
using System.Text.Json;
using Echo.Wire;

namespace Echo.Core;

public sealed class KeyStore(string path, IKeyProtector protector)
{
	private readonly object _lock = new object();

	public StoredCredentials? Load()
	{
		lock (_lock)
		{
			try
			{
				if (!File.Exists(path))
				{
					return null;
				}
				return JsonSerializer.Deserialize<StoredCredentials>(protector.Unprotect(File.ReadAllBytes(path)), WireJson.Options);
			}
			catch
			{
				return null;
			}
		}
	}

	public void Save(StoredCredentials creds)
	{
		lock (_lock)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
			byte[] bytes = protector.Protect(JsonSerializer.SerializeToUtf8Bytes(creds, WireJson.Options));
			string temp = path + ".tmp";
			try
			{
				File.WriteAllBytes(temp, bytes);
				File.Move(temp, path, overwrite: true);
			}
			catch
			{
				try
				{
					File.Delete(temp);
				}
				catch
				{
				}
				throw;
			}
		}
	}
}
