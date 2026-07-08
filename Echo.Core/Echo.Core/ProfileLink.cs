using System;

namespace Echo.Core;

public static class ProfileLink
{
	public const string BaseUrl = "https://echovault.gg";

	public static string For(uint worldId, string name)
	{
		return $"{"https://echovault.gg"}/p/{worldId}/{Uri.EscapeDataString(name.ToLowerInvariant().Replace(' ', '_'))}";
	}
}
