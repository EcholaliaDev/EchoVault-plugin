using System;
using System.Security.Cryptography;
using System.Text;

namespace Echo.Wire;

public static class HmacSigner
{
	public static string Sha256Hex(ReadOnlySpan<byte> data)
	{
		return Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
	}

	public static string Canonical(string method, string pathAndQuery, string bodySha256Hex, long unixTimestamp, string nonce)
	{
		return $"{method.ToUpperInvariant()}\n{pathAndQuery}\n{bodySha256Hex}\n{unixTimestamp}\n{nonce}";
	}

	public static string Sign(byte[] hmacSecret, string canonical)
	{
		return Convert.ToHexString(HMACSHA256.HashData(hmacSecret, Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
	}

	public static bool Verify(byte[] hmacSecret, string canonical, string signatureHex)
	{
		byte[] expected = HMACSHA256.HashData(hmacSecret, Encoding.UTF8.GetBytes(canonical));
		byte[] provided;
		try
		{
			provided = Convert.FromHexString(signatureHex);
		}
		catch (FormatException)
		{
			return false;
		}
		return CryptographicOperations.FixedTimeEquals(expected, provided);
	}

	public static string NewNonce()
	{
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
	}
}
