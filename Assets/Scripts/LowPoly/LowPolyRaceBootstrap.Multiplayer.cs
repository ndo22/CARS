using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lokální multiplayer: menu pro 2-4 hráče (auto, barva, ovládání), rozdělení obrazovky,
/// vyřazovačka (poslední vypadne po každém kole vedoucího) a limit 30 s po dojetí prvního hráče.
/// </summary>
public partial class LowPolyRaceBootstrap
{
    private static int multiplayerTab;

    private bool Elimination => RaceSetup.mode == RaceSetup.Mode.Multiplayer && Multiplayer.game == Multiplayer.Game.Elimination;
    private bool MultiplayerRace => RaceSetup.mode == RaceSetup.Mode.Multiplayer && Multiplayer.game == Multiplayer.Game.Race;

    private readonly List<RacerBase> eliminatedOrder = new List<RacerBase>();
    private float graceEnd = -1f;
    private bool graceWarned;
    private CameraFollow overviewFollow;

    /// <summary>Barva auta lidského hráče (pruh v HUD, tečka na minimapě, výsledky).</summary>
    private Color HumanColor(int h) => SplitScreen ? CarColors[Multiplayer.colors[Mathf.Clamp(h, 0, Multiplayer.MaxPlayers - 1)]] : CarColors[RaceSetup.colorIndex];

    private int AvailableColors => Profile.IsColorUnlocked(CarColors.Length - 1) ? CarColors.Length : 8;

    // ================================================================ menu

    /// <summary>Hráč 1 má auto a barvu z garáže; ostatní hráči mají odemčená auta a každý jinou barvu.</summary>
    private void SyncMultiplayerCars()
    {
        Multiplayer.styles[0] = RaceSetup.carStyle;
        Multiplayer.colors[0] = RaceSetup.colorIndex;
        int count = Multiplayer.MaxPlayers;
        for (int i = 1; i < count; i++)
        {
            if (!Profile.IsUnlocked(Multiplayer.styles[i])) Multiplayer.styles[i] = LowPolyCarFactory.BodyStyle.Sport;
            bool clash = Multiplayer.colors[i] >= AvailableColors;
            for (int j = 0; j < i; j++) clash |= Multiplayer.colors[j] == Multiplayer.colors[i];
            if (clash) Multiplayer.colors[i] = Multiplayer.NextFreeColor(i, AvailableColors, count);
        }
    }

    private void BuildMultiplayerScreen()
    {
        RaceSetup.mode = RaceSetup.Mode.Multiplayer;
        SyncMultiplayerCars();
        Transform root = menuCanvas.transform;

        Text title = LowPolyUI.Label(root, "MULTIPLAYER", 64, TextAnchor.UpperLeft, Color.white, "Title");
        LowPolyUI.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -40f), new Vector2(900f, 80f));
        string sub = "SPLIT SCREEN FOR 2 TO 4 PLAYERS";
        if (Multiplayer.roundsPlayed > 0) sub += "   |   MATCH   " + Multiplayer.MatchScore(Multiplayer.Players);
        Text subtitle = LowPolyUI.Label(root, sub, 24, TextAnchor.UpperLeft, LowPolyUI.Accent, "Subtitle", true);
        LowPolyUI.Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(52f, -112f), new Vector2(1400f, 30f));

        BuildTrackList(root, false, RaceSetup.Screen.Multiplayer);

        RectTransform right = LowPolyUI.Panel(root, "MultiplayerPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(right, new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(580f, 700f));
        ChoiceRow(right, 30f, -16f, new[] { "PLAYERS", "RACE" }, multiplayerTab, 255f, i =>
        {
            multiplayerTab = i;
            ShowScreen(RaceSetup.Screen.Multiplayer);
        }, 50f, 24);

        statusText = LowPolyUI.Label(right, "", 18, TextAnchor.LowerCenter, LowPolyUI.Accent, "Status", true);
        LowPolyUI.Place(statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(540f, 26f));

        if (multiplayerTab == 0) BuildPlayersTab(right);
        else BuildRaceTab(right, false);

        // --- start
        RectTransform bottom = LowPolyUI.Panel(root, "StartPanel", LowPolyUI.PanelColor);
        LowPolyUI.Place(bottom, new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(580f, 150f));
        Header(bottom, "LAPS", -14f);
        if (Multiplayer.game == Multiplayer.Game.Race)
        {
            ChoiceRow(bottom, 30f, -50f, new[] { "1", "3", "5" }, System.Array.IndexOf(RaceSetup.LapOptions, RaceSetup.laps), 70f,
                i => RaceSetup.laps = RaceSetup.LapOptions[i], 62f, 26);
        }
        else
        {
            Text laps = LowPolyUI.Label(bottom, "ONE CAR OUT\nEVERY LAP", 20, TextAnchor.UpperLeft, Color.white, "LapsInfo", true);
            LowPolyUI.Place(laps.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -52f), new Vector2(240f, 60f));
        }
        Image race = AccentButton(bottom, "RACE!", 46, OnRacePressed);
        LowPolyUI.Place((RectTransform)race.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(270f, 104f));

        Image back = LowPolyUI.Button(root, "< BACK", 28, () => ShowScreen(RaceSetup.Screen.Title));
        LowPolyUI.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 70f));
    }

    private void BuildPlayersTab(RectTransform right)
    {
        float y = -84f;
        Header(right, "PLAYERS", y);
        ChoiceRow(right, 30f, y - 34f, new[] { "2", "3", "4" }, Multiplayer.Players - 2, 72f, i =>
        {
            Multiplayer.players = i + 2;
            ShowScreen(RaceSetup.Screen.Multiplayer);
        });
        Text hint = LowPolyUI.Label(right, "CLICK A COLOR OR A CAR\nTO CHANGE IT", 16, TextAnchor.MiddleLeft, Dim, "PlayersHint", true);
        LowPolyUI.Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(290f, y - 34f), new Vector2(260f, 48f));

        y -= 100f;
        int count = Multiplayer.Players;
        for (int p = 0; p < count; p++)
        {
            int index = p;
            float rowY = y - p * 62f;
            Text tag = LowPolyUI.Label(right, "P" + (p + 1), 30, TextAnchor.MiddleLeft, Color.white, "Tag" + p);
            LowPolyUI.Place(tag.rectTransform, new Vector2(0f, 1f), new Vector2(30f, rowY), new Vector2(60f, 50f));

            Image swatch = LowPolyUI.Button(right, "", 10, null);
            swatch.color = CarColors[Multiplayer.colors[p]];
            swatch.GetComponent<Button>().onClick.AddListener(() =>
            {
                Multiplayer.colors[index] = Multiplayer.NextFreeColor(index, AvailableColors, count);
                swatch.color = CarColors[Multiplayer.colors[index]];
                if (index == 0)
                {
                    RaceSetup.colorIndex = Multiplayer.colors[0];
                    SpawnPreviewCar();
                }
            });
            LowPolyUI.Place((RectTransform)swatch.transform, new Vector2(0f, 1f), new Vector2(90f, rowY), new Vector2(50f, 50f));

            Image car = LowPolyUI.Button(right, LowPolyCarFactory.StyleNames[(int)Multiplayer.styles[p]], 22, null, out Text carLabel);
            car.GetComponent<Button>().onClick.AddListener(() =>
            {
                int s = (int)Multiplayer.styles[index];
                do { s = (s + 1) % LowPolyCarFactory.StyleNames.Length; } while (!Profile.IsUnlocked((LowPolyCarFactory.BodyStyle)s));
                Multiplayer.styles[index] = (LowPolyCarFactory.BodyStyle)s;
                carLabel.text = LowPolyCarFactory.StyleNames[s];
                if (index == 0)
                {
                    RaceSetup.carStyle = Multiplayer.styles[0];
                    SpawnPreviewCar();
                }
            });
            LowPolyUI.Place((RectTransform)car.transform, new Vector2(0f, 1f), new Vector2(152f, rowY), new Vector2(190f, 50f));

            Text keys = LowPolyUI.Label(right, Multiplayer.Controls[p], 16, TextAnchor.MiddleLeft, Dim, "Keys" + p, true);
            LowPolyUI.Place(keys.rectTransform, new Vector2(0f, 1f), new Vector2(358f, rowY), new Vector2(200f, 50f));
        }

        y -= count * 62f + 16f;
        Header(right, "GAME", y);
        ChoiceRow(right, 30f, y - 34f, new[] { "RACE", "ELIMINATION" }, (int)Multiplayer.game, 255f, i =>
        {
            Multiplayer.game = (Multiplayer.Game)i;
            ShowScreen(RaceSetup.Screen.Multiplayer);
        }, 48f, 22);
        string rules = Multiplayer.game == Multiplayer.Game.Race
            ? "FIRST ACROSS THE LINE WINS   -   THE OTHERS THEN HAVE 30 S"
            : "THE LAST CAR IS OUT EVERY LAP   -   LAST ONE LEFT WINS";
        Text rulesText = LowPolyUI.Label(right, rules, 16, TextAnchor.UpperLeft, Dim, "Rules", true);
        LowPolyUI.Place(rulesText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, y - 92f), new Vector2(540f, 22f));
    }

    // ================================================================ závod

    /// <summary>Oddělovače výřezů, přehledová kamera (3 hráči) a společná minimapa (3-4 hráči).</summary>
    private void SetupSplitScreenExtras()
    {
        int count = humans.Count;
        Bar(new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 6f));
        if (count >= 3) Bar(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(6f, 0f));

        if (count == 3)
        {
            // Volná čtvrtina: kamera shora nad vedoucím autem
            var go = new GameObject("OverviewCamera");
            Camera cam = SetupCamera(go);
            cam.rect = new Rect(0.5f, 0f, 0.5f, 0.5f);
            overviewFollow = go.AddComponent<CameraFollow>();
            overviewFollow.Target = humans[0].transform;
            overviewFollow.offset = new Vector3(0f, 55f, -34f);
            overviewFollow.eulerrotation = new Vector3(58f, 0f, 0f);
            overviewFollow.damper = 2.5f;
            go.transform.position = humans[0].transform.position + overviewFollow.offset;
            go.transform.rotation = Quaternion.Euler(overviewFollow.eulerrotation);
            Text leader = LowPolyUI.Label(raceCanvas.transform, "LEADER", 26, TextAnchor.UpperLeft, LowPolyUI.Accent, "LeaderLabel");
            LowPolyUI.Place(leader.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(24f, -16f), new Vector2(300f, 36f));
        }

        if (count >= 3)
        {
            Minimap minimap = Minimap.Create((RectTransform)raceCanvas.transform, track.centers, settings.roadWidth, count == 3 ? 240f : 180f);
            var rect = (RectTransform)minimap.transform;
            // 3 hráči: v rohu volné čtvrtiny; 4 hráči: dole uprostřed (střed obrazovky je roh HUD hráče 4)
            Vector2 anchor = count == 3 ? new Vector2(1f, 0f) : new Vector2(0.5f, 0f);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = count == 3 ? new Vector2(-24f, 24f) : new Vector2(0f, 16f);
            foreach (var ai in opponents)
                minimap.AddRacer(ai.transform, ai.GetComponentInChildren<MeshRenderer>().sharedMaterials[0].color, false);
            for (int h = 0; h < count; h++)
                minimap.AddRacer(humans[h].transform, HumanColor(h), true);
        }
    }

    private void Bar(Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var go = new GameObject("SplitBar", typeof(RectTransform));
        go.transform.SetParent(raceCanvas.transform, false);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.02f, 0.03f, 0.06f, 1f);
        image.raycastTarget = false;
        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.SetAsFirstSibling();
    }

    /// <summary>Závodníci, kteří ještě nevypadli.</summary>
    private List<RacerBase> ActiveRacers()
    {
        var list = new List<RacerBase>();
        foreach (var h in humans) if (!eliminatedOrder.Contains(h)) list.Add(h);
        foreach (var ai in opponents) if (!eliminatedOrder.Contains(ai)) list.Add(ai);
        return list;
    }

    private RacerBase Leader()
    {
        RacerBase best = null;
        foreach (var r in ActiveRacers())
            if (best == null || r.RaceProgress > best.RaceProgress) best = r;
        return best;
    }

    /// <summary>Vedoucí dokončil kolo - vyřadit tolik posledních aut, kolik už mělo vypadnout.</summary>
    private void OnEliminationLap(RacerBase racer)
    {
        int racers = humans.Count + opponents.Count;
        int due = Multiplayer.EliminationsDue(racer.CurrentLap, racers);
        while (eliminatedOrder.Count < due && !resultsShown)
        {
            List<RacerBase> active = ActiveRacers();
            var progress = new List<float>();
            var racing = new List<bool>();
            foreach (var r in active) { progress.Add(r.RaceProgress); racing.Add(true); }
            int out_ = Multiplayer.PickEliminated(progress, racing);
            if (out_ < 0) break;
            Eliminate(active[out_]);
        }
    }

    private void Eliminate(RacerBase racer)
    {
        eliminatedOrder.Add(racer);
        int place = humans.Count + opponents.Count - eliminatedOrder.Count + 1;
        Vector3 at = racer.transform.position;
        if (ParticleFX.Instance != null)
        {
            for (int k = 0; k < 8; k++) ParticleFX.Instance.Smoke(at + Random.insideUnitSphere * 1.2f, Vector3.up * 2f);
            ParticleFX.Instance.Sparks(at + Vector3.up, Vector3.up, 16);
        }
        foreach (var h in huds)
        {
            if (h.player == racer) h.ui.ShowMessage("ELIMINATED\n" + Ordinal(place), 3f, new Color(1f, 0.35f, 0.3f));
            else if (!eliminatedOrder.Contains(h.player)) h.ui.ShowMessage(racer.DisplayName + " IS OUT", 1.8f, LowPolyUI.Accent);
        }
        racer.Controller.InputEnabled = false;
        racer.gameObject.SetActive(false);

        List<RacerBase> left = ActiveRacers();
        bool humansLeft = left.Exists(r => r is Player);
        if ((left.Count <= 1 || !humansLeft) && !resultsShown)
            StartCoroutine(ShowResults());
    }

    /// <summary>Každý snímek: diváci vyřazených hráčů sledují vedoucího, přehledová kamera taky; limit po dojetí prvního.</summary>
    private void UpdateMultiplayer()
    {
        if (!SplitScreen || resultsShown) return;
        RacerBase leader = Leader();
        if (leader != null)
        {
            if (overviewFollow != null) overviewFollow.Target = leader.transform;
            foreach (var h in huds)
            {
                if (!eliminatedOrder.Contains(h.player) || h.follow == null) continue;
                if (h.follow.target != leader.transform)
                {
                    h.follow.target = leader.transform;
                    if (h.cameraFx != null) h.cameraFx.target = leader.Controller;
                }
            }
        }

        if (graceEnd > 0f && !PauseMenu.GameIsPaused)
        {
            float left = graceEnd - Time.time;
            if (!graceWarned && left <= 10f)
            {
                graceWarned = true;
                foreach (var h in huds)
                    if (!finishOrder.Contains(h.player)) h.ui.ShowMessage("10 S LEFT!", 1.6f, new Color(1f, 0.35f, 0.3f));
            }
            if (left <= 0f) StartCoroutine(ShowResults());
        }
    }

    /// <summary>První hráč dojel - ostatní mají 30 s.</summary>
    private void StartFinishGrace()
    {
        if (!MultiplayerRace || graceEnd > 0f) return;
        graceEnd = Time.time + Multiplayer.FinishGrace;
        foreach (var h in huds)
            if (!finishOrder.Contains(h.player))
                h.ui.ShowMessage($"{Mathf.RoundToInt(Multiplayer.FinishGrace)} S TO FINISH", 2f, LowPolyUI.Accent);
    }
}
