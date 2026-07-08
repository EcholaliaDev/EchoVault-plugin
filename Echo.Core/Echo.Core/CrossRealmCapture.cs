namespace Echo.Core;

public static class CrossRealmCapture
{
	public static CapturedPlayer? TryMap(ulong contentId, string name, short homeWorld, short currentWorld, byte jobId, byte level, string? homeWorldName)
	{
		if (contentId == 0L || homeWorld <= 0)
		{
			return null;
		}
		uint current = (uint)((currentWorld > 0) ? currentWorld : homeWorld);
		return new CapturedPlayer(contentId, name, (uint)homeWorld, current, 0, 0f, 0f, 0f, jobId, level, null, null, "social", 0uL, 0, 0, null, 0uL, 0uL, homeWorldName);
	}
}
