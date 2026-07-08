using System.Text.RegularExpressions;

namespace Echo.Wire;

public static class Protocol
{
	public const int Version = 2;

	public const int MaxBatchSize = 200;

	public const ulong MinRetailContentId = 18014398509481984uL;

	public const int MaxNameLength = 21;

	public static readonly Regex NameShape = new Regex("^[A-Z][a-zA-Z'\\-]{1,14} [A-Z][a-zA-Z'\\-]{1,14}$", RegexOptions.Compiled);

	public static bool IsValidCharacterName(string name)
	{
		if (name.Length <= 21)
		{
			return NameShape.IsMatch(name);
		}
		return false;
	}

	public static string FoldApostrophes(string s)
	{
		return s.Replace('’', '\'').Replace('‘', '\'').Replace('ʼ', '\'');
	}
}
