using System.Globalization;
using UnityEngine;

/// <summary>Formátování a parsování časů kol (m:ss.fff).</summary>
public static class TimeFormat
{
    public static string Format(float seconds)
    {
        if (float.IsInfinity(seconds) || float.IsNaN(seconds) || seconds < 0f)
            return "--:--.---";

        // Počítat v milisekundách, ať se nikdy nezobrazí "1:60.000"
        int totalMs = Mathf.RoundToInt(seconds * 1000f);
        int minutes = totalMs / 60000;
        int secs = (totalMs / 1000) % 60;
        int ms = totalMs % 1000;
        return $"{minutes}:{secs:00}.{ms:000}";
    }

    /// <summary>
    /// Závodní zápis 1'05"123 - používá se v low-poly UI, protože písmo Aquire nemá dvojtečku.
    /// </summary>
    public static string FormatRacing(float seconds)
    {
        if (float.IsInfinity(seconds) || float.IsNaN(seconds) || seconds < 0f)
            return "-'--\"---";
        int totalMs = Mathf.RoundToInt(seconds * 1000f);
        return $"{totalMs / 60000}'{(totalMs / 1000) % 60:00}\"{totalMs % 1000:000}";
    }

    /// <summary>Načte čas ve formátu "m:ss.fff" (i se starší desetinnou čárkou). Při chybě vrátí false.</summary>
    public static bool TryParse(string text, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrEmpty(text)) return false;

        string[] parts = text.Replace(',', '.').Split(':');
        if (parts.Length != 2) return false;

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float secs)) return false;

        seconds = minutes * 60f + secs;
        return true;
    }
}
