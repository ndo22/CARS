/// <summary>
/// Volby ze startovní obrazovky low-poly závodu. Statické, takže přežijí znovunačtení scény (restart závodu).
/// </summary>
public static class RaceSetup
{
    /// <summary>true = hráč už si vybral a scéna má rovnou spustit závod (např. po restartu).</summary>
    public static bool configured;

    public static int trackIndex;
    public static LowPolyCarFactory.BodyStyle carStyle = LowPolyCarFactory.BodyStyle.Sport;
    public static int colorIndex;
    public static int opponents = 3;
    public static int difficulty = 1; // 0 lehká, 1 normální, 2 těžká
    public static int laps = 3;

    public static readonly int[] OpponentOptions = { 1, 3, 5 };
    public static readonly int[] LapOptions = { 1, 3, 5 };
    public static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD" };
    public static readonly float[] DifficultySkill = { 0.82f, 0.93f, 1.03f };
}
