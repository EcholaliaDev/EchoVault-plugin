namespace Echo.Core;

public static class LinkClaimMessages
{
	public static string Describe(LinkStartError? error)
	{
		if ((object)error != null)
		{
			string other = error.Error;
			switch (other)
			{
			case "standing":
				return "this account can't claim yet. It needs " + (error.AgeRequiredDays?.ToString() ?? "a") + " day(s) of age and " + (error.ObservedRequired?.ToString() ?? "10") + "+ players seen (currently " + (error.Observed?.ToString() ?? "?") + "). Keep playing with Echo running, then try again.";
			case "banned":
				return "this account has been disabled by the server.";
			case "protocol_too_old":
				return "the server requires a newer Echo - please update the plugin.";
			case "not_registered":
				return "not registered with the server yet - Echo registers automatically on its first upload; play for a minute and try again.";
			case "bad_content_id":
			case "bad_character_name":
				return "could not read your character cleanly - try again in a moment.";
			case "unreachable":
				break;
			default:
				return "claim code request failed (" + other.Replace('_', ' ') + ").";
			}
		}
		return "the server did not respond - try again in a minute.";
	}
}
