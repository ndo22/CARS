using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Volby z low-poly menu. Statické, takže přežijí znovunačtení scény (restart závodu, další kolo šampionátu).
/// </summary>
public static class RaceSetup
{
    public enum Screen { Title, Setup, Settings, Editor, Achievements }
    public enum Mode { QuickRace, Championship, TimeTrial }

    /// <summary>true = hráč už si vybral a scéna má rovnou spustit závod (např. po restartu).</summary>
    public static bool configured;
    /// <summary>Kterou obrazovku menu ukázat, když se závod nespouští rovnou.</summary>
    public static Screen screen = Screen.Title;
    public static Mode mode = Mode.QuickRace;

    public static int trackIndex;
    public static LowPolyCarFactory.BodyStyle carStyle = LowPolyCarFactory.BodyStyle.Sport;
    public static int colorIndex;
    public static int opponents = 3;
    public static int difficulty = 1; // 0 lehká, 1 normální, 2 těžká
    public static int laps = 3;
    public static bool night;
    public static bool rain;
    /// <summary>Podmínky se během závodu mění: z dne do noci / ze sucha do deště.</summary>
    public static bool dynamicTime, dynamicWeather;
    /// <summary>Počet hráčů (2 = split-screen, jen v rychlém závodě).</summary>
    public static int players = 1;
    public static int carColor2 = 1;
    public static LowPolyCarFactory.BodyStyle carStyle2 = LowPolyCarFactory.BodyStyle.Muscle;
    /// <summary>Kosmetika: vzor laku, barva disků, startovní číslo.</summary>
    public static int pattern, rims, number = 7;
    /// <summary>Index vlastní tratě z editoru (-1 = vestavěná).</summary>
    public static int customTrack = -1;

    public static readonly int[] OpponentOptions = { 1, 3, 5 };
    public static readonly int[] LapOptions = { 1, 3, 5 };
    public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };
    public static readonly float[] DifficultySkill = { 0.82f, 0.93f, 1.03f };

    public static int Opponents => mode == Mode.TimeTrial ? 0 : opponents;
    public static int Players => mode == Mode.QuickRace ? Mathf.Clamp(players, 1, 2) : 1;
}

/// <summary>Šampionát: série všech tratí, body za umístění.</summary>
public static class Championship
{
    public static readonly int[] Points = { 10, 6, 4, 3, 2, 1 };
    public static readonly string[] AiNames = { "VIPER", "NOVA", "BLAZE", "ROCKET", "DASH" };

    public static bool active;
    public static int round;
    public static readonly Dictionary<string, int> standings = new Dictionary<string, int>();

    public static int Rounds => TrackLibrary.All.Length;

    public static void Start()
    {
        active = true;
        round = 0;
        standings.Clear();
    }

    public static void AddResult(string name, int position)
    {
        if (!standings.ContainsKey(name)) standings[name] = 0;
        if (position >= 1 && position <= Points.Length) standings[name] += Points[position - 1];
    }

    public static List<KeyValuePair<string, int>> Sorted()
    {
        var list = new List<KeyValuePair<string, int>>(standings);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    public static bool IsLastRound => round >= Rounds - 1;
}

/// <summary>Postup hráče: výhry a odemčená auta (PlayerPrefs).</summary>
public static class Profile
{
    public static int Wins { get => PlayerPrefs.GetInt("profileWins", 0); set { PlayerPrefs.SetInt("profileWins", value); PlayerPrefs.Save(); } }
    public static int Championships { get => PlayerPrefs.GetInt("profileTitles", 0); set { PlayerPrefs.SetInt("profileTitles", value); PlayerPrefs.Save(); } }

    public static bool IsUnlocked(LowPolyCarFactory.BodyStyle style)
    {
        switch (style)
        {
            case LowPolyCarFactory.BodyStyle.Pickup: return Wins >= 1;
            case LowPolyCarFactory.BodyStyle.Buggy: return Wins >= 3;
            case LowPolyCarFactory.BodyStyle.Formula: return Championships >= 1;
            default: return true;
        }
    }

    public static string UnlockHint(LowPolyCarFactory.BodyStyle style)
    {
        switch (style)
        {
            case LowPolyCarFactory.BodyStyle.Pickup: return "WIN 1 RACE";
            case LowPolyCarFactory.BodyStyle.Buggy: return "WIN 3 RACES";
            case LowPolyCarFactory.BodyStyle.Formula: return "WIN A CHAMPIONSHIP";
            default: return "";
        }
    }

    /// <summary>Speciální barvy (zlatá, chromová) se odemknou vítězstvím v šampionátu.</summary>
    public static bool IsColorUnlocked(int index) => index < 8 || Championships >= 1;
}
