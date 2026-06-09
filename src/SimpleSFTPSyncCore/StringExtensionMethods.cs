namespace SimpleSFTPSyncCore;

public static class StringExtensionMethods
{
	public static string ToTitleCase(this string str)
	{
		if (string.IsNullOrWhiteSpace(str))
		{
			return str;
		}

		var tokens = str.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		for (var i = 0; i < tokens.Length; i++)
		{
			var token = tokens[i];
			tokens[i] = string.Concat(token[..1].ToUpperInvariant(), token[1..]);
		}

		return string.Join(" ", tokens);
	}
}
