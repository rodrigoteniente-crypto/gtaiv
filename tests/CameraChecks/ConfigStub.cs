using System.Globalization;
using System.Text;

namespace KickChaos;

// CameraStore only needs normalization from Config. The full Config also
// references the game's input API, which is unavailable in these offline checks.
// Keep this helper equivalent to Config.Normalize; no GTA natives are simulated.
public static class Config
{
    public static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (char character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
