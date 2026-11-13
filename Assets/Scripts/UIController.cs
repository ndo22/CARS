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

    [Header("Start závodu")]
    [Tooltip("Odpočet 3-2-1-GO před závodem.")]
    public bool useCountdown = true;
    public int countdownFrom = 3;

    private int currentLap = -1;
    private float currentTime = -1f;
    private float lastLapTime = -1f;
    private float bestLapTime = -1f;
    private float bestAILapTime = -1f;

    private Text speedText;
    private Text positionText;
    private Text centerText;
    private float centerMessageUntil;

    private void Start()
    {
        if (UpdateUIForPlayer != null)
            UpdateUIForPlayer.LapCompleted += OnPlayerLapCompleted;

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
            ShowMessage($"NEW BEST LAP!\n{TimeFormat.Format(lapTime)}", 2.5f, new Color(1f, 0.85f, 0.2f));
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
                SetText(UITextCurrentLap, $"Lap: {currentLap}");
            }

            if (UpdateUIForPlayer.CurrentLapTime != currentTime)
            {
                currentTime = UpdateUIForPlayer.CurrentLapTime;
                SetText(UITextCurrentTime, $"Time: {TimeFormat.Format(currentTime)}");
            }

            if (UpdateUIForPlayer.LastLapTime != lastLapTime)
            {
                lastLapTime = UpdateUIForPlayer.LastLapTime;
                SetText(UITextLastLap, lastLapTime > 0f ? $"Last: {TimeFormat.Format(lastLapTime)}" : "Last: NONE");
            }

            if (UpdateUIForPlayer.BestLapTime != bestLapTime)
            {
                bestLapTime = UpdateUIForPlayer.BestLapTime;
                SetText(UITextBestLap, !float.IsInfinity(bestLapTime) ? $"Best: {TimeFormat.Format(bestLapTime)}" : "Best: NONE");
            }

            if (speedText != null && UpdateUIForPlayer.Controller != null)
                speedText.text = $"{Mathf.RoundToInt(UpdateUIForPlayer.Controller.SpeedKmh)} km/h";
        }

        if (UpdateUIForAIPlayer != null)
        {
            if (UpdateUIForAIPlayer.BestLapTime != bestAILapTime)
            {
                bestAILapTime = UpdateUIForAIPlayer.BestLapTime;
                SetText(UITextAIBestLap, !float.IsInfinity(bestAILapTime) ? $"CPU Best: {TimeFormat.Format(bestAILapTime)}" : "CPU Best: NONE");
            }
        }

        if (positionText != null && UpdateUIForPlayer != null && UpdateUIForAIPlayer != null)
        {
            int position = UpdateUIForPlayer.RaceProgress >= UpdateUIForAIPlayer.RaceProgress ? 1 : 2;
            positionText.text = $"P{position}/2";
            positionText.color = position == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
        }

        // Při pauze schovat překryvné texty, ať nepřekrývají menu
        bool showOverlay = !PauseMenu.GameIsPaused;
        if (speedText != null) speedText.enabled = showOverlay;
        if (positionText != null) positionText.enabled = showOverlay;
        if (centerText != null) centerText.enabled = showOverlay && Time.time < centerMessageUntil;
    }

    private void ShowMessage(string message, float duration, Color color)
    {
        if (centerText == null) return;
        centerText.text = message;
        centerText.color = color;
        centerMessageUntil = Time.time + duration;
    }

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
            new Vector2(1f, 0f), new Vector2(-30f, 30f));
        positionText = CreateText("PositionText", canvas, template, 40, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-30f, -30f));
        centerText = CreateText("CenterMessage", canvas, template, 90, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 80f));
        centerText.enabled = false;
    }

    private static Text CreateText(string name, Transform parent, Text template, int size, TextAnchor alignment,
        Vector2 anchor, Vector2 offset)
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
        text.font = template.font;
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
