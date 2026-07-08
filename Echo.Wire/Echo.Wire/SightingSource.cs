namespace Echo.Wire;

public static class SightingSource
{
	public const string Sweep = "sweep";

	public const string Spawn = "spawn";

	public const string Social = "social";

	public const string NameCache = "namecache";

	public static bool IsValid(string s)
	{
		switch (s)
		{
		case "sweep":
		case "spawn":
		case "social":
		case "namecache":
			return true;
		default:
			return false;
		}
	}
}
