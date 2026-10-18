using System.Text;

/// <summary>
/// Шифр Цезаря для латиницы и кириллицы. Регистр сохраняется,
/// цифры, знаки и пробелы не меняются, теги TextMeshPro (&lt;color=...&gt;) пропускаются.
/// </summary>
public static class CaesarCipher
{
    private const string LatinUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string LatinLower = "abcdefghijklmnopqrstuvwxyz";
    private const string CyrillicUpper = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";
    private const string CyrillicLower = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";

    /// <summary>Сдвинуть буквы на shift позиций. Отрицательный сдвиг — расшифровка.</summary>
    public static string Shift(string text, int shift)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";

        var result = new StringBuilder(text.Length);
        bool insideTag = false;

        foreach (char c in text)
        {
            if (c == '<') insideTag = true;

            if (insideTag)
            {
                result.Append(c);
                if (c == '>') insideTag = false;
                continue;
            }

            result.Append(ShiftChar(c, shift));
        }

        return result.ToString();
    }

    private static char ShiftChar(char c, int shift)
    {
        foreach (string alphabet in new[] { LatinUpper, LatinLower, CyrillicUpper, CyrillicLower })
        {
            int index = alphabet.IndexOf(c);
            if (index < 0) continue;

            int n = alphabet.Length;
            int shifted = ((index + shift) % n + n) % n;
            return alphabet[shifted];
        }

        return c;
    }
}
