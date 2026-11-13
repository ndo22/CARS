using UnityEngine;

/// <summary>
/// Asistence pro začátečníky: jemně dotáčí auto ke středu trati a automaticky přibrzdí před ostrou zatáčkou.
/// Síla se nastavuje v menu (GameSettings.SteerAssist / BrakeAssist).
/// </summary>
public class DrivingAssist : MonoBehaviour
{
    [Range(0f, 1f)] public float steering = 0.4f;
    public bool braking = true;
    public float cornerSpeedKmh = 70f;

    private RacerBase racer;
    private CarController car;
    private Transform[] checkpoints;

    private void Start()
    {
        racer = GetComponent<RacerBase>();
        car = GetComponent<CarController>();
        checkpoints = CheckpointSet.Find();
    }

    public void Apply(ref float gas, ref float brake, ref float steer)
    {
        if (racer == null || car == null || checkpoints == null || checkpoints.Length < 3) return;
        int next = racer.NextCheckpointIndex;
        Transform target = checkpoints[next % checkpoints.Length];
        Transform after = checkpoints[(next + 1) % checkpoints.Length];

        // Řízení: přimíchat směr k dalšímu checkpointu, když hráč sám moc nezatáčí
        Vector3 aim = Vector3.Lerp(target.position, after.position, 0.35f);
        Vector3 local = transform.InverseTransformPoint(aim);
        float suggested = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg / Mathf.Max(1f, car.maxSteer), -1f, 1f);
        if (local.z > 0f)
        {
            float own = Mathf.Abs(steer);
            steer = Mathf.Lerp(steer, suggested, steering * (1f - own * 0.7f));
        }

        // Brzdění: zpomalit na rozumnou rychlost před ostrou zatáčkou
        if (braking)
        {
            Vector3 d1 = target.position - transform.position, d2 = after.position - target.position;
            d1.y = d2.y = 0f;
            float corner = Vector3.Angle(d1, d2);
            float distance = d1.magnitude;
            float safe = Mathf.Lerp(car.maxSpeedKmh, cornerSpeedKmh, Mathf.Clamp01(corner / 45f)) + distance * 1.5f;
            if (car.ForwardSpeedKmh > safe + 8f)
            {
                gas = Mathf.Min(gas, 0.2f);
                brake = Mathf.Max(brake, Mathf.Clamp01((car.ForwardSpeedKmh - safe) / 40f));
            }
        }
    }
}
