using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Hlídá úspěchy během jízdy (drift, čisté kolo, nitro, rychlost) a zobrazuje oznámení o odemčení.</summary>
public class AchievementTracker : MonoBehaviour
{
    public CarController car;
    public RacerBase racer;
    public Text toast;

    private float driftTime;
    private bool lapClean = true;
    private float nitroUnsaved;
    private readonly Queue<string> messages = new Queue<string>();
    private float toastTimer;

    private void Start()
    {
        Achievements.Unlocked += OnUnlocked;
        car.Impact += strength => { if (strength > 3f) lapClean = false; };
        racer.LapStarted += () => lapClean = true;
        racer.LapCompleted += t =>
        {
            if (lapClean && racer.CurrentLap >= 1) Achievements.Unlock("clean_lap");
        };
    }

    private void OnDestroy()
    {
        Achievements.Unlocked -= OnUnlocked;
        if (nitroUnsaved > 0f) Achievements.NitroSeconds += nitroUnsaved;
    }

    private void OnUnlocked(Achievements.Definition def)
    {
        messages.Enqueue("ACHIEVEMENT   " + def.name + (string.IsNullOrEmpty(def.reward) ? "" : "\nUNLOCKED  " + def.reward));
    }

    private void Update()
    {
        if (car == null) return;

        if (car.DriftAmount > 0.35f) driftTime += Time.deltaTime;
        else driftTime = 0f;
        if (driftTime >= 5f) Achievements.Unlock("drift_king");

        if (car.SpeedKmh >= 240f) Achievements.Unlock("speed_demon");

        if (car.NitroActive)
        {
            nitroUnsaved += Time.deltaTime;
            if (nitroUnsaved > 5f)
            {
                Achievements.NitroSeconds += nitroUnsaved;
                nitroUnsaved = 0f;
                if (Achievements.NitroSeconds >= 60f) Achievements.Unlock("nitro_junkie");
            }
        }

        // Oznámení
        if (toast == null) return;
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f) toast.enabled = false;
        }
        else if (messages.Count > 0)
        {
            toast.text = messages.Dequeue();
            toast.enabled = true;
            toastTimer = 3.5f;
        }
    }
}
