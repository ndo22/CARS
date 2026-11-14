using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Úspěchy (výzvy) uložené v PlayerPrefs. Některé odemykají kosmetiku.</summary>
public static class Achievements
{
    public class Definition
    {
        public string id, name, description, reward;
        public Definition(string id, string name, string description, string reward = "")
        {
            this.id = id; this.name = name; this.description = description; this.reward = reward;
        }
    }

    public static readonly Definition[] All =
    {
        new Definition("first_win", "FIRST WIN", "WIN ANY RACE", "BANDS PATTERN"),
        new Definition("hat_trick", "HAT TRICK", "WIN 3 RACES", "GOLD RIMS"),
        new Definition("champion", "CHAMPION", "WIN A CHAMPIONSHIP", "CHECKER PATTERN"),
        new Definition("drift_king", "DRIFT KING", "DRIFT FOR 5 SECONDS IN ONE GO", "RED RIMS"),
        new Definition("clean_lap", "CLEAN LAP", "FINISH A LAP WITHOUT HITTING ANYTHING"),
        new Definition("nitro_junkie", "NITRO JUNKIE", "USE NITRO FOR 60 SECONDS IN TOTAL"),
        new Definition("speed_demon", "SPEED DEMON", "REACH 240 KMH"),
        new Definition("night_rider", "NIGHT RIDER", "WIN A RACE AT NIGHT"),
        new Definition("rain_man", "RAIN MASTER", "WIN A RACE IN THE RAIN"),
        new Definition("ghostbuster", "GHOSTBUSTER", "BEAT YOUR GHOST IN TIME TRIAL"),
        new Definition("world_tour", "WORLD TOUR", "RACE ON EVERY TRACK"),
        new Definition("architect", "ARCHITECT", "SAVE A TRACK IN THE EDITOR"),
        new Definition("getaway", "GETAWAY", "ESCAPE THE POLICE IN 5 CHASE LEVELS", "MONSTER TRUCK"),
        new Definition("most_wanted", "MOST WANTED", "FINISH 10 CHASE LEVELS", "POLICE CAR"),
        new Definition("legend", "LEGEND", "FINISH ALL 15 CHASE LEVELS", "ROCKET CAR"),
        new Definition("high_flyer", "HIGH FLYER", "JUMP OVER 30 METERS"),
        new Definition("wrecking_crew", "WRECKING CREW", "SMASH 100 BARRIERS, CRATES OR ROADBLOCKS"),
    };

    /// <summary>Vyvolá se při odemčení (pro oznámení ve hře).</summary>
    public static event Action<Definition> Unlocked;

    public static bool Has(string id) => PlayerPrefs.GetInt("ach_" + id, 0) == 1;

    public static int Count
    {
        get
        {
            int n = 0;
            foreach (var a in All) if (Has(a.id)) n++;
            return n;
        }
    }

    public static void Unlock(string id)
    {
        if (Has(id)) return;
        PlayerPrefs.SetInt("ach_" + id, 1);
        PlayerPrefs.Save();
        Definition def = Array.Find(All, a => a.id == id);
        if (def != null) Unlocked?.Invoke(def);
    }

    /// <summary>Zapamatuje si trať, na které hráč jel (pro WORLD TOUR).</summary>
    public static void TrackVisited(string trackId, IEnumerable<string> allIds)
    {
        PlayerPrefs.SetInt("visited_" + trackId, 1);
        foreach (string id in allIds)
            if (PlayerPrefs.GetInt("visited_" + id, 0) == 0) return;
        Unlock("world_tour");
    }

    /// <summary>Hráč prorazil zničitelný objekt (svodidlo, bednu, zátaras, pneumatiky).</summary>
    public static void ObjectSmashed()
    {
        int n = PlayerPrefs.GetInt("smashed", 0) + 1;
        PlayerPrefs.SetInt("smashed", n);
        if (n >= 100) Unlock("wrecking_crew");
    }

    public static int Smashed => PlayerPrefs.GetInt("smashed", 0);

    public static float NitroSeconds
    {
        get => PlayerPrefs.GetFloat("nitroSeconds", 0f);
        set => PlayerPrefs.SetFloat("nitroSeconds", value);
    }
}

/// <summary>Kosmetika aut: vzory laku a barvy disků, některé se odemykají úspěchy.</summary>
public static class Cosmetics
{
    public static readonly string[] Patterns = { "NONE", "STRIPES", "BANDS", "CHECKER" };
    public static readonly string[] Rims = { "SILVER", "BLACK", "GOLD", "RED" };
    public static readonly Color[] RimColors =
    {
        new Color(0.72f, 0.74f, 0.78f), new Color(0.12f, 0.12f, 0.13f), new Color(0.95f, 0.72f, 0.2f), new Color(0.85f, 0.12f, 0.1f),
    };

    public static bool PatternUnlocked(int i) => i <= 1 || (i == 2 && Achievements.Has("first_win")) || (i == 3 && Achievements.Has("champion"));
    public static bool RimUnlocked(int i) => i <= 1 || (i == 2 && Achievements.Has("hat_trick")) || (i == 3 && Achievements.Has("drift_king"));

    public static string PatternHint(int i) => i == 2 ? "WIN A RACE" : i == 3 ? "WIN A CHAMPIONSHIP" : "";
    public static string RimHint(int i) => i == 2 ? "WIN 3 RACES" : i == 3 ? "DRIFT FOR 5 SECONDS" : "";
}
