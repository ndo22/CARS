using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Závodní HUD: kolo, časy, rychloměr, pořadí, odpočet na startu a hlášky (nejlepší kolo).
/// Rychloměr, pořadí a odpočet se vytváří za běhu a přebírají font z existujícího textu,
/// takže ve scéně není potřeba nic přidávat.
/// </summary>
public class UIController : MonoBehaviour
{
    public GameObject UIRacePanel;

    public Text UITextCurrentLap;
    public Text UITextCurrentTime;
    public Text UITextLastLap;
    public Text UITextBestLap;
    public Text UITextAIBestLap;

    public Player UpdateUIForPlayer;
    public AIPlayer UpdateUIForAIPlayer;

    public Leaderboard leaderboard;

    [Tooltip("Počet kol závodu (0 = bez omezení). Zobrazí se jako 'Lap: 2/3'.")]
    public int totalLaps = 0;

    [Tooltip("Styl textů pro písmo bez ':' a '/' (low-poly scéna): 'LAP 2 OF 3', 1'05\"123, 'P1 OF 4'.")]
    public bool lowPolyStyle;
    [Tooltip("Písmo pro rychloměr, pořadí a hlášky (prázdné = podle textu kola).")]
    public Font overlayFont;

    [Header("Start závodu")]
    [Tooltip("Odpočet 3-2-1-GO před závodem.")]
    public bool useCountdown = true;
    public int countdownFrom = 3;

    private int currentLap = -1;
    private float currentTime = -1f;
    private float lastLapTime = -1f;
    private float bestLapTime = -1f;
    private float bestAILapTime = -1f;

    private RacerBase[] racers = new RacerBase[0];
    private AIPlayer[] aiRacers = new AIPlayer[0];

    private Text speedText;
    private Text positionText;
    private Text centerText;
    private float centerMessageUntil;

    private void Start()
    {
        if (UpdateUIForPlayer != null)
            UpdateUIForPlayer.LapCompleted += OnPlayerLapCompleted;

        racers = FindObjectsOfType<RacerBase>();
        aiRacers = FindObjectsOfType<AIPlayer>();
        CreateRuntimeTexts();

        if (useCountdown)
            StartCoroutine(Countdown());
    }

    private void OnDestroy()
    {
        if (UpdateUIForPlayer != null)
            UpdateUIForPlayer.LapCompleted -= OnPlayerLapCompleted;
    }

    private void OnPlayerLapCompleted(float lapTime)
    {
        bool isNewBest = lapTime <= UpdateUIForPlayer.BestLapTime;
        if (leaderboard != null)
            leaderboard.AddLeaderboardEntry(lapTime);

        if (isNewBest && UpdateUIForPlayer.CurrentLap > 1)
            ShowMessage($"NEW BEST LAP!\n{(lowPolyStyle ? Fmt(lapTime) : TimeFormat.Format(lapTime))}", 2.5f, new Color(1f, 0.85f, 0.2f));
    }

    private IEnumerator Countdown()
    {
        CarController[] cars = FindObjectsOfType<CarController>();
        foreach (var car in cars) car.InputEnabled = false;

        // Chvilka na načtení scény
        yield return new WaitForSeconds(0.5f);

        for (int i = countdownFrom; i > 0; i--)
        {
            ShowMessage(i.ToString(), 1.1f, Color.white);
            yield return new WaitForSeconds(1f);
        }

        foreach (var car in cars) car.InputEnabled = true;
        ShowMessage("GO!", 1f, new Color(0.3f, 1f, 0.4f));
    }

    private void Update()
    {
        if (UpdateUIForPlayer != null)
        {
            if (UpdateUIForPlayer.CurrentLap != currentLap)
            {
                currentLap = UpdateUIForPlayer.CurrentLap;
                if (lowPolyStyle)
                    SetText(UITextCurrentLap, totalLaps > 0 ? $"LAP {Mathf.Clamp(currentLap, 1, totalLaps)} OF {totalLaps}" : $"LAP {currentLap}");
                else
                    SetText(UITextCurrentLap, totalLaps > 0
                        ? $"Lap: {Mathf.Clamp(currentLap, 1, totalLaps)}/{totalLaps}"
                        : $"Lap: {currentLap}");
            }

            if (UpdateUIForPlayer.CurrentLapTime != currentTime)
            {
                currentTime = UpdateUIForPlayer.CurrentLapTime;
                SetText(UITextCurrentTime, lowPolyStyle ? Fmt(currentTime) : $"Time: {TimeFormat.Format(currentTime)}");
            }

            if (UpdateUIForPlayer.LastLapTime != lastLapTime)
            {
                lastLapTime = UpdateUIForPlayer.LastLapTime;
                SetText(UITextLastLap, lowPolyStyle ? $"LAST  {(lastLapTime > 0f ? Fmt(lastLapTime) : Fmt(-1f))}" : (lastLapTime > 0f ? $"Last: {TimeFormat.Format(lastLapTime)}" : "Last: NONE"));
            }

            if (UpdateUIForPlayer.BestLapTime != bestLapTime)
            {
                bestLapTime = UpdateUIForPlayer.BestLapTime;
                SetText(UITextBestLap, lowPolyStyle ? $"BEST  {Fmt(bestLapTime)}" : (!float.IsInfinity(bestLapTime) ? $"Best: {TimeFormat.Format(bestLapTime)}" : "Best: NONE"));
            }

            if (speedText != null && UpdateUIForPlayer.Controller != null)
                speedText.text = lowPolyStyle ? $"{Mathf.RoundToInt(UpdateUIForPlayer.Controller.SpeedKmh)} KMH" : $"{Mathf.RoundToInt(UpdateUIForPlayer.Controller.SpeedKmh)} km/h";
        }

        // Nejlepší kolo ze všech počítačových soupeřů
        float aiBest = UpdateUIForAIPlayer != null ? UpdateUIForAIPlayer.BestLapTime : Mathf.Infinity;
        foreach (var ai in aiRacers)
            if (ai != null) aiBest = Mathf.Min(aiBest, ai.BestLapTime);
        if (aiBest != bestAILapTime)
        {
            bestAILapTime = aiBest;
            SetText(UITextAIBestLap, lowPolyStyle ? $"CPU   {Fmt(bestAILapTime)}" : (!float.IsInfinity(bestAILapTime) ? $"CPU Best: {TimeFormat.Format(bestAILapTime)}" : "CPU Best: NONE"));
        }

        if (positionText != null && UpdateUIForPlayer != null && racers.Length > 1)
        {
            int position = Position(UpdateUIForPlayer);
            positionText.text = lowPolyStyle ? $"P{position} OF {racers.Length}" : $"P{position}/{racers.Length}";
            positionText.color = position == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
        }

        // Při pauze schovat překryvné texty, ať nepřekrývají menu
        bool showOverlay = !PauseMenu.GameIsPaused;
        if (speedText != null) speedText.enabled = showOverlay;
        if (positionText != null) positionText.enabled = showOverlay;
        if (centerText != null) centerText.enabled = showOverlay && Time.time < centerMessageUntil;
    }

    /// <summary>Pořadí závodníka (1 = první) podle ujeté vzdálenosti.</summary>
    public int Position(RacerBase racer)
    {
        int position = 1;
        float progress = racer.RaceProgress;
        foreach (var other in racers)
            if (other != null && other != racer && other.RaceProgress > progress)
                position++;
        return position;
    }

    /// <summary>Krátká hláška uprostřed obrazovky.</summary>
    public void ShowMessage(string message, float duration, Color color)
    {
        if (centerText == null) return;
        centerText.text = message;
        centerText.color = color;
        centerMessageUntil = Time.time + duration;
    }

    private static string Fmt(float seconds) => TimeFormat.FormatRacing(seconds);

    private static void SetText(Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private void CreateRuntimeTexts()
    {
        Text template = UITextCurrentLap != null ? UITextCurrentLap : UITextCurrentTime;
        if (template == null || template.canvas == null)
            return;

        Transform canvas = template.canvas.transform;

        speedText = CreateText("SpeedText", canvas, template, 40, TextAnchor.LowerRight,
            new Vector2(1f, 0f), new Vector2(-30f, 30f), overlayFont);
        positionText = CreateText("PositionText", canvas, template, 40, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-30f, -30f), overlayFont);
        centerText = CreateText("CenterMessage", canvas, template, 90, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), overlayFont);
        centerText.enabled = false;
    }

    private static Text CreateText(string name, Transform parent, Text template, int size, TextAnchor alignment,
        Vector2 anchor, Vector2 offset, Font font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(800f, 250f);

        var text = go.AddComponent<Text>();
        text.font = font != null ? font : template.font;
        text.fontStyle = FontStyle.Bold;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }
}
