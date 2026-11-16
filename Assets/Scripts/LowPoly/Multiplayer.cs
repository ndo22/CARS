using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lokální multiplayer (split-screen pro 2-4 hráče): volby, rozložení obrazovky, pravidla vyřazovačky a skóre zápasu.
/// Čistá logika bez Unity objektů - testuje se v EditMode testech.
/// </summary>
public static class Multiplayer
{
    public enum Game
    {
        Race,        // klasický závod, po dojetí prvního hráče mají ostatní 30 s
        Elimination  // po každém kole vedoucího vypadne poslední, vyhrává poslední, kdo zbyde
    }

    public const int MaxPlayers = 4;
    /// <summary>Kolik sekund mají ostatní hráči na dojetí, když první dojede do cíle.</summary>
    public const float FinishGrace = 30f;

    public static int players = 2;
    public static Game game = Game.Race;
    public static int opponents;
    public static readonly int[] OpponentOptions = { 0, 1, 3 };

    public static readonly LowPolyCarFactory.BodyStyle[] styles =
    {
        LowPolyCarFactory.BodyStyle.Sport, LowPolyCarFactory.BodyStyle.Muscle, LowPolyCarFactory.BodyStyle.Hatch, LowPolyCarFactory.BodyStyle.Sport
    };
    public static readonly int[] colors = { 0, 1, 2, 3 };

    /// <summary>Výhry hráčů v aktuálním zápase (série odvet). Nuluje se při vstupu do menu multiplayeru.</summary>
    public static readonly int[] matchWins = new int[MaxPlayers];
    public static int roundsPlayed;

    public static int Players => Mathf.Clamp(players, 2, MaxPlayers);

    public static readonly string[] Controls = { "WASD   PAD 1", "ARROWS   PAD 2", "IJKL   PAD 3", "NUMPAD   PAD 4" };

    public static void ResetMatch()
    {
        for (int i = 0; i < matchWins.Length; i++) matchWins[i] = 0;
        roundsPlayed = 0;
    }

    public static void RecordWin(int playerIndex)
    {
        roundsPlayed++;
        if (playerIndex >= 0 && playerIndex < matchWins.Length) matchWins[playerIndex]++;
    }

    /// <summary>Text skóre zápasu, např. "P1 2   P2 1   P3 0".</summary>
    public static string MatchScore(int count)
    {
        var parts = new List<string>();
        for (int i = 0; i < Mathf.Clamp(count, 1, MaxPlayers); i++) parts.Add($"P{i + 1} {matchWins[i]}");
        return string.Join("   ", parts);
    }

    /// <summary>
    /// Výřez obrazovky (Camera.rect) hráče. 2 hráči: nad sebou. 3-4 hráči: čtvrtiny
    /// (P1 vlevo nahoře, P2 vpravo nahoře, P3 vlevo dole, P4 vpravo dole; u 3 hráčů zbyde čtvrtina na přehled).
    /// </summary>
    public static Rect Viewport(int index, int count)
    {
        if (count <= 1) return new Rect(0f, 0f, 1f, 1f);
        if (count == 2) return index == 0 ? new Rect(0f, 0.5f, 1f, 0.5f) : new Rect(0f, 0f, 1f, 0.5f);
        float x = index % 2 == 0 ? 0f : 0.5f;
        float y = index < 2 ? 0.5f : 0f;
        return new Rect(x, y, 0.5f, 0.5f);
    }

    /// <summary>
    /// Kdo má vypadnout: z jezdců, kteří ještě jedou, ten s nejmenším postupem závodem.
    /// Vrací index do seznamu nebo -1, když jede nanejvýš jeden.
    /// </summary>
    public static int PickEliminated(IList<float> progress, IList<bool> stillRacing)
    {
        int worst = -1, racing = 0;
        for (int i = 0; i < progress.Count; i++)
        {
            if (!stillRacing[i]) continue;
            racing++;
            if (worst < 0 || progress[i] < progress[worst]) worst = i;
        }
        return racing > 1 ? worst : -1;
    }

    /// <summary>Kolik jezdců už mělo vypadnout, když vedoucí dokončil daný počet kol (jeden za kolo, až zbyde jeden).</summary>
    public static int EliminationsDue(int leaderLaps, int racers) => Mathf.Clamp(leaderLaps, 0, Mathf.Max(0, racers - 1));

    /// <summary>Další barva, kterou nemá žádný jiný hráč (cyklí přes <paramref name="available"/> barev).</summary>
    public static int NextFreeColor(int player, int available, int count)
    {
        int c = colors[player];
        for (int step = 0; step < available; step++)
        {
            c = (c + 1) % available;
            bool used = false;
            for (int i = 0; i < count; i++) used |= i != player && colors[i] == c;
            if (!used) return c;
        }
        return colors[player];
    }
}
