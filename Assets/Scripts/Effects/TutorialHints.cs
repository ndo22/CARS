using UnityEngine;
using UnityEngine.UI;

/// <summary>Krátký návod při prvním závodě: postupně vysvětlí plyn, zatáčení, drift, nitro a reset.</summary>
public class TutorialHints : MonoBehaviour
{
    public CarController car;
    public RacerBase racer;
    public Text text;

    private int step;
    private float stepTime;
    private float driftTime;

    private static string Key(GameInput.Action a) => GameInput.GetKey(a).ToString().ToUpper().Replace("LEFTSHIFT", "SHIFT").Replace("ALPHA", "");

    private void Update()
    {
        if (car == null || text == null) return;
        stepTime += Time.deltaTime;
        text.enabled = !PauseMenu.GameIsPaused && car.InputEnabled;

        switch (step)
        {
            case 0:
                Show($"HOLD {Key(GameInput.Action.Accelerate)} OR RT TO ACCELERATE");
                if (car.SpeedKmh > 40f) Next();
                break;
            case 1:
                Show($"STEER WITH {Key(GameInput.Action.Left)} AND {Key(GameInput.Action.Right)}   -   STAY ON THE ROAD, GRASS SLOWS YOU DOWN");
                if (racer.LastCheckpointPassed >= 4 || stepTime > 15f) Next();
                break;
            case 2:
                Show($"IN A CORNER HOLD {Key(GameInput.Action.Handbrake)} TO DRIFT");
                if (car.DriftAmount > 0.35f) driftTime += Time.deltaTime;
                if (driftTime > 0.6f || stepTime > 25f) Next();
                break;
            case 3:
                Show($"DRIFTING CHARGES NITRO   -   PRESS {Key(GameInput.Action.Nitro)} TO BOOST");
                if (car.NitroActive || stepTime > 20f) Next();
                break;
            case 4:
                Show($"STUCK OR UPSIDE DOWN?   PRESS {Key(GameInput.Action.Reset)}   -   GOOD LUCK!");
                if (stepTime > 5f) Next();
                break;
            default:
                GameSettings.TutorialDone = true;
                Destroy(text.gameObject);
                Destroy(this);
                break;
        }
    }

    private void Show(string message)
    {
        if (text.text != message) text.text = message;
    }

    private void Next()
    {
        step++;
        stepTime = 0f;
    }
}
