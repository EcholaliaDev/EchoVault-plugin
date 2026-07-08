namespace Echo.Core;

public interface IKeyProtector
{
	byte[] Protect(byte[] data);

	byte[] Unprotect(byte[] data);
}
