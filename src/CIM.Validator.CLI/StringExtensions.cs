using System.Text;

namespace CIM.Validator.CLI;

public static class StringExtensions
{
    public static string ToSnakeCaseUpper(this string text)
    {
        ArgumentNullException.ThrowIfNull(text, nameof(text));

        if (text.Length < 2)
        {
            return text.ToUpperInvariant();
        }

        var sb = new StringBuilder();

        sb.Append(char.ToLowerInvariant(text[0]));

        for (int i = 1; i < text.Length; ++i)
        {
            char c = text[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString().ToUpperInvariant();
    }
}

