namespace Echo.Core;

public sealed class PlaintextKeyProtector : IKeyProtector
{
	public byte[] Protect(byte[] data)
	{
		return data;
	}

	public byte[] Unprotect(byte[] data)
	{
		return data;
	}
}
