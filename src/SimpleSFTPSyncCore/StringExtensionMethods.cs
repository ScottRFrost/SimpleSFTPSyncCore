using System;

namespace SimpleSFTPSyncCore
{
    public static class StringExtensionMethods
    {
        public static string ToTitleCase(this string str)
        {
            var tokens = str.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i];
                tokens[i] = token.Substring(0, 1).ToUpper() + token[1..];
            }

            return string.Join(" ", tokens);
        }
    }
}
