using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Společná logika závodníka (hráč i počítač): průjezd checkpointy, počítání kol, časy a návrat na trať.
/// Checkpointy jsou potomci objektu "Checkpoints" pojmenovaní 1, 2, 3, ... (1 = start/cíl).
/// </summary>
public abstract class RacerBase : MonoBehaviour
{
    public float BestLapTime { get; private set; } = Mathf.Infinity;
    public float LastLapTime { get; private set; } = 0;
    public float CurrentLapTime { get; private set; } = 0;
    public int CurrentLap { get; private set; } = 0;
    public int LastCheckpointPassed => lastCheckpointPassed;

    /// <summary>Vyvolá se po dokončení kola, parametr je čas kola v sekundách.</summary>
    public event Action<float> LapCompleted;

    protected Transform[] checkpoints = new Transform[0];
    protected int CheckpointCount => checkpoints.Length;

    private float lapStartTime = -1f;
    private int lastCheckpointPassed = 0;
    private int checkpointLayer;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public CarController Controller { get; private set; }

    protected virtual void Awake()
    {
        checkpointLayer = LayerMask.NameToLayer("Checkpoint");
        Controller = GetComponent<CarController>();
        startPosition = transform.position;
        startRotation = transform.rotation;

        var parent = GameObject.Find("Checkpoints");
        if (parent == null)
        {
            Debug.LogError("Ve scéně chybí objekt 'Checkpoints'.");
            return;
        }

        // Seřadit podle čísla v názvu, ať nezáleží na pořadí v hierarchii
        var list = new List<Transform>();
        foreach (Transform child in parent.transform)
        {
            if (int.TryParse(child.name, out _))
                list.Add(child);
        }
        checkpoints = list.OrderBy(t => int.Parse(t.name)).ToArray();
    }

    protected virtual void Update()
    {
        CurrentLapTime = lapStartTime >= 0f ? Time.time - lapStartTime : 0f;

        // Pád mimo mapu
        if (transform.position.y < fallRespawnHeight)
            Respawn();
    }

    [Tooltip("Pod touto výškou se auto automaticky vrátí na trať.")]
    public float fallRespawnHeight = -50f;

    /// <summary>Index (v poli checkpoints) checkpointu, kterým má závodník projet jako dalším.</summary>
    public int NextCheckpointIndex => CheckpointCount == 0 ? 0 : lastCheckpointPassed % CheckpointCount;

    /// <summary>Spojitý ukazatel postupu v závodě - slouží k určení pořadí.</summary>
    public float RaceProgress
    {
        get
        {
            if (CheckpointCount == 0) return 0f;
            int passed = Mathf.Max(0, CurrentLap - 1) * CheckpointCount + lastCheckpointPassed;
            Transform next = checkpoints[NextCheckpointIndex];
            Transform prev = lastCheckpointPassed > 0 ? checkpoints[lastCheckpointPassed - 1] : null;
            float fraction = 0f;
            if (prev != null)
            {
                float segment = Vector3.Distance(prev.position, next.position);
                float remaining = Vector3.Distance(transform.position, next.position);
                fraction = segment > 0.01f ? Mathf.Clamp01(1f - remaining / segment) : 0f;
            }
            else
            {
                // Před startovní čárou - čím blíž, tím lépe
                fraction = -Vector3.Distance(transform.position, next.position) / 10000f;
            }
            return passed + fraction;
        }
    }

    private void StartLap()
    {
        CurrentLap++;
        lastCheckpointPassed = 1;
        lapStartTime = Time.time;
    }

    private void EndLap()
    {
        LastLapTime = Time.time - lapStartTime;
        BestLapTime = Mathf.Min(LastLapTime, BestLapTime);
        LapCompleted?.Invoke(LastLapTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer != checkpointLayer || CheckpointCount == 0)
            return;

        if (other.gameObject.name == "1")
        {
            bool lapFinished = lastCheckpointPassed == CheckpointCount;
            if (lapFinished)
                EndLap();
            if (CurrentLap == 0 || lapFinished)
                StartLap();
            return;
        }

        // Checkpointy se musí projet popořadě, jinak se nepočítají (žádné zkratky)
        if (other.gameObject.name == (lastCheckpointPassed + 1).ToString())
            lastCheckpointPassed++;
    }

    /// <summary>Vrátí auto na trať k poslednímu projetému checkpointu, natočené směrem k dalšímu.</summary>
    public void Respawn()
    {
        if (Controller == null) return;

        Vector3 position = startPosition;
        Quaternion rotation = startRotation;

        if (lastCheckpointPassed > 0 && CheckpointCount > 0)
        {
            Transform from = checkpoints[lastCheckpointPassed - 1];
            Transform to = checkpoints[NextCheckpointIndex];
            position = from.position;

            Vector3 dir = to.position - from.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // Najít povrch trati pod checkpointem (auta ignorovat, ať se nespawneme na soupeře)
            int mask = Physics.DefaultRaycastLayers & ~LayerMask.GetMask("Car", "Car2", "Wheels");
            if (Physics.Raycast(position + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 50f,
                    mask, QueryTriggerInteraction.Ignore))
                position = hit.point;
        }

        Controller.Teleport(position + Vector3.up * 1f, rotation);
    }
}
