using UnityEngine;

/// <summary>
/// Levely režimu POLICE CHASE a postup hráče (hvězdy, nejlepší časy) v PlayerPrefs.
/// Speciální auta: MONSTER za 5 dokončených levelů, POLICE za 10, ROCKET za všech 15.
/// </summary>
public static class ChaseLevels
{
    public const int Count = 15;

    private static readonly string[] Names =
    {
        "COUNTRY ESCAPE", "CANYON RUN", "ICE BREAKER", "BEACH BREAK", "NEON GETAWAY",
    };

    /// <summary>Téma levelu - prostředí se střídají (louka, poušť, sníh, pobřeží, noční město).</summary>
    public static int ThemeIndex(int level) => (level - 1) % 5;

    public static string Name(int level) => Names[ThemeIndex(level)] + " " + ((level - 1) / 5 + 1);

    /// <summary>Kolik policejních aut honí hráče.</summary>
    public static int PoliceCount(int level) => level >= 12 ? 3 : level >= 6 ? 2 : 1;

    /// <summary>Jak rychlá je policie (násobek maximální rychlosti policejního auta).</summary>
    public static float PoliceSkill(int level) => Mathf.Lerp(0.8f, 0.98f, (level - 1) / 14f);

    /// <summary>Časy na hvězdy podle délky dráhy (od startu hráče do cíle): 3 hvězdy průměr ~83 km/h, 2 hvězdy ~65 km/h.</summary>
    public static float ThreeStarTime(float length) => length / 23f;
    public static float TwoStarTime(float length) => length / 18f;

    public static int StarsFor(float time, float length)
    {
        if (time <= ThreeStarTime(length)) return 3;
        if (time <= TwoStarTime(length)) return 2;
        return 1;
    }

    public static int Stars(int level) => PlayerPrefs.GetInt("chase_stars_" + level, 0);
    public static float BestTime(int level) => PlayerPrefs.GetFloat("chase_time_" + level, Mathf.Infinity);
    public static bool Unlocked(int level) => level <= 1 || Stars(level - 1) > 0;

    public static int Completed
    {
        get
        {
            int n = 0;
            for (int i = 1; i <= Count; i++) if (Stars(i) > 0) n++;
            return n;
        }
    }

    public static int TotalStars
    {
        get
        {
            int n = 0;
            for (int i = 1; i <= Count; i++) n += Stars(i);
            return n;
        }
    }

    /// <summary>Uloží výsledek; vrátí true, když je to nový nejlepší čas.</summary>
    public static bool Save(int level, int stars, float time)
    {
        if (stars > Stars(level)) PlayerPrefs.SetInt("chase_stars_" + level, stars);
        bool best = time < BestTime(level);
        if (best) PlayerPrefs.SetFloat("chase_time_" + level, time);
        PlayerPrefs.Save();
        return best;
    }

    /// <summary>Speciální auto, které se odemyká dokončením daného počtu levelů (nebo null).</summary>
    public static int RequiredFor(LowPolyCarFactory.BodyStyle style)
    {
        switch (style)
        {
            case LowPolyCarFactory.BodyStyle.Monster: return 5;
            case LowPolyCarFactory.BodyStyle.Police: return 10;
            case LowPolyCarFactory.BodyStyle.Rocket: return 15;
            default: return 0;
        }
    }
}
