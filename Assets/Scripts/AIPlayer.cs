using UnityEngine;

/// <summary>
/// Počítačový soupeř. Jede po checkpointech, před zatáčkami brzdí podle jejich ostrosti,
/// a když se zasekne nebo převrátí, nejdřív zkusí vycouvat a pak se vrátí na trať.
/// </summary>
public class AIPlayer : RacerBase
{
    [Header("Rychlost")]
    [Tooltip("Rychlost na rovince v km/h.")]
    public float straightSpeedKmh = 150f;
    [Tooltip("Rychlost v ostré (90°) zatáčce v km/h.")]
    public float cornerSpeedKmh = 55f;
    [Tooltip("Úhel zatáčky (mezi dvěma úseky checkpointů), při kterém AI zpomalí až na cornerSpeedKmh.")]
    public float cornerAngleForMinSpeed = 90f;
    [Tooltip("Kolik checkpointů dopředu AI sleduje zatáčky. Pro hustě rozmístěné checkpointy dej víc.")]
    public int cornerLookAhead = 2;
    [Tooltip("O kolik km/h může být AI rychlejší za každý metr před zatáčkou (jak prudce brzdí).")]
    public float brakingSlope = 1.4f;

    [Header("Řízení")]
    [Tooltip("Jak daleko před checkpointem se AI začne natáčet k dalšímu (m). Vyhlazuje průjezd zatáčkou.")]
    public float lookAheadDistance = 15f;
    [Tooltip("Obtížnost - násobí cílovou rychlost (0.7 = lehká, 1 = normální, 1.15 = těžká).")]
    [Range(0.5f, 1.3f)] public float skill = 1f;

    [Header("Souboj")]
    [Tooltip("Jak daleko dopředu AI hlídá auta, která by mohla předjet nebo nabourat (m).")]
    public float avoidDistance = 20f;
    [Tooltip("Použít nitro na rovinkách.")]
    public bool useNitro = true;

    [Header("Zaseknutí")]
    public float stuckTime = 1.5f;
    public float reverseTime = 1.2f;
    [Tooltip("Kolikrát zkusí vycouvat, než se vrátí na trať.")]
    public int maxReverseAttempts = 3;

    public AICar carController { get; private set; }

    private float stuckTimer;
    private float reverseTimer;
    private float flippedTimer;
    private int stuckAttempts;

    protected override void Awake()
    {
        base.Awake();
        carController = GetComponent<AICar>();
    }

    protected override void Update()
    {
        base.Update();
        if (carController == null || CheckpointCount == 0)
            return;

        Transform target = checkpoints[NextCheckpointIndex];
        Transform after = checkpoints[(NextCheckpointIndex + 1) % CheckpointCount];

        Vector3 position = transform.position;
        Vector3 toTarget = Flat(target.position - position);
        float distance = toTarget.magnitude;

        // Blízko checkpointu už míříme k dalšímu, ať AI neprojíždí zatáčky ostře "do špičky"
        float blend = Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, lookAheadDistance));
        Vector3 aimPoint = Vector3.Lerp(target.position, after.position, blend * 0.5f);

        // Vyhýbání: auto před námi -> posunout cíl do strany (předjíždění), případně ubrat
        float followSpeed = float.MaxValue;
        Vector3 avoidShift = AvoidTraffic(target, ref followSpeed);
        aimPoint += avoidShift;

        Vector3 local = transform.InverseTransformPoint(aimPoint);
        float angleToAim = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float steer = Mathf.Clamp(angleToAim / Mathf.Max(1f, carController.maxSteer), -1f, 1f);

        // Projít několik dalších úseků mezi checkpointy a najít nejpomalejší zatáčku,
        // přičemž vzdálenější zatáčka dovolí jet zatím rychleji (stihne se ubrzdit)
        float desiredSpeed = straightSpeedKmh;
        float distanceToCorner = distance;
        Vector3 previousDir = toTarget;
        for (int j = 0; j < Mathf.Max(1, cornerLookAhead); j++)
        {
            Vector3 a = checkpoints[(NextCheckpointIndex + j) % CheckpointCount].position;
            Vector3 b = checkpoints[(NextCheckpointIndex + j + 1) % CheckpointCount].position;
            Vector3 segment = Flat(b - a);
            float cornerAngle = Vector3.Angle(previousDir, segment);
            float cornerSpeed = Mathf.Lerp(straightSpeedKmh, cornerSpeedKmh, Mathf.Clamp01(cornerAngle / Mathf.Max(1f, cornerAngleForMinSpeed)));
            desiredSpeed = Mathf.Min(desiredSpeed, cornerSpeed + distanceToCorner * brakingSlope);
            distanceToCorner += segment.magnitude;
            previousDir = segment;
        }
        // Když auto zrovna hodně zatáčí, ubrat
        desiredSpeed *= Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(Mathf.Abs(angleToAim) / 60f));
        desiredSpeed *= skill;
        desiredSpeed = Mathf.Min(desiredSpeed, followSpeed);

        float speed = carController.ForwardSpeedKmh;
        float throttle;
        float brake = 0f;

        if (speed < desiredSpeed)
            throttle = 1f;
        else if (speed > desiredSpeed + 8f)
        {
            throttle = 0f;
            brake = Mathf.Clamp01((speed - desiredSpeed) / 30f);
        }
        else
            throttle = 0.2f;

        // Checkpoint za zády (např. po odražení) - otočit se
        if (local.z < 0f && distance < 30f)
        {
            throttle = 0.6f;
            steer = Mathf.Sign(angleToAim);
        }

        HandleStuck(ref throttle, ref steer, ref brake);

        // Nitro na rovince, když je vpředu volno a zatáčka daleko
        carController.UseNitro = useNitro && carController.Nitro > 0.25f && throttle >= 1f && brake <= 0f
            && Mathf.Abs(angleToAim) < 6f && desiredSpeed >= straightSpeedKmh * skill * 0.95f && followSpeed == float.MaxValue;

        carController.Throttle = throttle;
        carController.Steer = steer;
        carController.Brake = brake;
    }

    private Vector3 AvoidTraffic(Transform target, ref float followSpeed)
    {
        Vector3 right = target.right;
        float halfWidth = RoadHalfWidth(NextCheckpointIndex);
        float bestDistance = avoidDistance;
        RacerBase blocker = null;

        foreach (RacerBase other in All)
        {
            if (other == null || other == this) continue;
            Vector3 local = transform.InverseTransformPoint(other.transform.position);
            if (local.z < 0f || local.z > avoidDistance || Mathf.Abs(local.x) > 3.2f) continue;
            if (local.z < bestDistance)
            {
                bestDistance = local.z;
                blocker = other;
            }
        }
        if (blocker == null) return Vector3.zero;

        // Předjet z té strany, kde je víc místa
        float myOffset = Vector3.Dot(transform.position - target.position, right);
        float blockerOffset = Vector3.Dot(blocker.transform.position - target.position, right);
        float side = blockerOffset > myOffset ? -1f : 1f;
        // Cíl je střed checkpointu, posun je tedy absolutní boční odsazení od středu silnice
        float shift = Mathf.Clamp(blockerOffset + side * 3.6f, -halfWidth, halfWidth);

        // Hodně blízko -> nenarážet zezadu
        var otherCar = blocker.GetComponent<CarController>();
        if (otherCar != null && bestDistance < 9f)
            followSpeed = Mathf.Max(20f, otherCar.ForwardSpeedKmh - 2f);

        float urgency = 1f - bestDistance / avoidDistance;
        return right * (shift * Mathf.Lerp(0.4f, 1f, urgency));
    }

    private void HandleStuck(ref float throttle, ref float steer, ref float brake)
    {
        if (!carController.InputEnabled || PauseMenu.GameIsPaused)
        {
            stuckTimer = 0f;
            reverseTimer = 0f;
            return;
        }

        // Převrácené auto
        if (Vector3.Dot(transform.up, Vector3.up) < 0.3f)
            flippedTimer += Time.deltaTime;
        else
            flippedTimer = 0f;

        if (flippedTimer > 2f)
        {
            flippedTimer = 0f;
            stuckTimer = 0f;
            Respawn();
            return;
        }

        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.deltaTime;
            throttle = -1f;
            steer = -steer;
            brake = 0f;
            return;
        }

        if (throttle > 0.5f && carController.SpeedKmh < 3f)
            stuckTimer += Time.deltaTime;
        else if (carController.SpeedKmh > 10f)
        {
            stuckTimer = 0f;
            stuckAttempts = 0;
        }

        if (stuckTimer > stuckTime)
        {
            stuckTimer = 0f;
            stuckAttempts++;
            if (stuckAttempts > maxReverseAttempts)
            {
                stuckAttempts = 0;
                Respawn();
            }
            else
                reverseTimer = reverseTime;
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
