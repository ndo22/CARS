using System;
using UnityEngine;

/// <summary>
/// Ovládání ve vzduchu (skoky): auto se samo srovnává do vodorovné polohy, nepřetáčí se kolem svislé osy
/// a hráč může plynem/brzdou naklánět předek (jako v arkádových závodech).
/// </summary>
public class AirControl : MonoBehaviour
{
    public float levelStrength = 2.5f;
    public float pitchControl = 2.2f;
    public int playerIndex = -1;
    public bool playerControlled = true;

    private CarController car;

    private void Start() => car = GetComponent<CarController>();

    private void FixedUpdate()
    {
        if (car == null) return;
        bool grounded = car.wheelColliderLeftFront.isGrounded || car.wheelColliderRightFront.isGrounded
                        || car.wheelColliderLeftBack.isGrounded || car.wheelColliderRightBack.isGrounded;
        if (grounded) return;

        Rigidbody rb = car.Body;
        Vector3 local = transform.InverseTransformDirection(rb.angularVelocity);
        float dt = Time.fixedDeltaTime;

        // Náklon do strany -> zpět k nule
        float roll = Mathf.DeltaAngle(0f, transform.eulerAngles.z) * Mathf.Deg2Rad;
        local.z = Mathf.Lerp(local.z, -roll * levelStrength, dt * 4f);
        // Otáčení kolem svislé osy tlumit
        local.y = Mathf.Lerp(local.y, 0f, dt * 2f);
        // Předek: hráč nahoru/dolů, jinak mírné srovnání
        float pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x) * Mathf.Deg2Rad;
        float input = playerControlled ? GameInput.Throttle(playerIndex) - GameInput.Brake(playerIndex) : 0f;
        float target = Mathf.Abs(input) > 0.1f ? -input * pitchControl : -pitch * levelStrength * 0.6f;
        local.x = Mathf.Lerp(local.x, target, dt * 3f);

        rb.angularVelocity = transform.TransformDirection(local);
    }
}

/// <summary>Policejní maják: střídavě červené a modré světlo a siréna.</summary>
public class PoliceLights : MonoBehaviour
{
    private Light red, blue;
    private AudioSource siren;

    private void Start()
    {
        red = MakeLight(new Vector3(-0.35f, 1.7f, -0.45f), new Color(1f, 0.1f, 0.1f));
        blue = MakeLight(new Vector3(0.35f, 1.7f, -0.45f), new Color(0.15f, 0.35f, 1f));
        siren = gameObject.AddComponent<AudioSource>();
        siren.clip = ProceduralAudio.Siren();
        siren.loop = true;
        siren.spatialBlend = 1f;
        siren.maxDistance = 140f;
        siren.rolloffMode = AudioRolloffMode.Linear;
        siren.volume = 0.5f * GameSettings.Sfx;
        siren.Play();
    }

    private Light MakeLight(Vector3 local, Color color)
    {
        var go = new GameObject("Beacon");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = 14f;
        l.intensity = 3f;
        l.shadows = LightShadows.None;
        return l;
    }

    private void Update()
    {
        bool phase = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
        if (red != null) red.enabled = phase;
        if (blue != null) blue.enabled = !phase;
        if (siren != null) siren.volume = PauseMenu.GameIsPaused ? 0f : 0.5f * GameSettings.Sfx;
    }
}

/// <summary>
/// Policejní AI pro překážkovou dráhu: jede po středové čáře, zblízka najíždí na hráče,
/// přizpůsobuje rychlost vzdálenosti (vzadu zrychlí, nitro), a když spadne nebo se zasekne,
/// objeví se znovu kus za hráčem.
/// </summary>
public class ChaseAI : MonoBehaviour
{
    public ObstacleCourse.Plan plan;
    public CarController target;
    [Range(0.5f, 1.3f)] public float skill = 0.9f;
    public float cornerSpeedKmh = 75f;
    /// <summary>Policie se rozjede až po odpočtu a této prodlevě.</summary>
    public bool released;

    private AICar car;
    private int index;
    private int targetIndex;
    private float stuckTimer, flipTimer, reverseTimer;

    public int Index => index;

    private void Start()
    {
        car = GetComponent<AICar>();
        index = plan.Nearest(transform.position, 0, 0, 30);
    }

    public void SetTargetIndex(int i) => targetIndex = i;

    private void Update()
    {
        if (car == null || plan == null) return;
        car.InputEnabled = released;
        if (!released) return;

        index = plan.Nearest(transform.position, index, 6, 30);
        float speed = car.ForwardSpeedKmh;

        // Kam mířit: kus dopředu po dráze, zblízka přímo na hráče
        int look = Mathf.Min(plan.Count - 1, index + 3 + Mathf.RoundToInt(speed / 30f));
        Vector3 aim = plan.points[look];
        float gap = plan.distance[Mathf.Clamp(targetIndex, 0, plan.Count - 1)] - plan.distance[index];
        if (target != null && gap > 0f && gap < 22f)
            aim = Vector3.Lerp(aim, target.transform.position, 0.7f);

        Vector3 local = transform.InverseTransformPoint(aim);
        float steer = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg / Mathf.Max(1f, car.maxSteer), -1f, 1f);

        // Rychlost: zatáčky přibrzdit, na skokankách naopak plnou rychlostí
        float desired = car.maxSpeedKmh * skill;
        float corner = 0f;
        for (int k = index; k < Mathf.Min(plan.Count - 2, index + 12); k++)
            corner = Mathf.Max(corner, Vector3.Angle(plan.Forward(index), plan.Forward(k + 1)));
        desired = Mathf.Min(desired, Mathf.Lerp(car.maxSpeedKmh, cornerSpeedKmh, Mathf.Clamp01(corner / 70f)));
        for (int k = index; k < Mathf.Min(plan.Count, index + 14); k++)
            if (plan.features[k] == ObstacleCourse.Feature.Ramp || plan.features[k] == ObstacleCourse.Feature.Gap)
                desired = Mathf.Max(desired, 105f);
        // Gumička: daleko za hráčem přidat, těsně za ním držet tempo
        if (gap > 60f) desired *= 1.15f;
        bool nitro = gap > 90f && Mathf.Abs(steer) < 0.2f;

        float throttle = speed < desired ? 1f : 0.15f;
        float brake = speed > desired + 12f ? Mathf.Clamp01((speed - desired) / 40f) : 0f;

        // Zaseknutí: chvíli couvat
        if (reverseTimer > 0f)
        {
            reverseTimer -= Time.deltaTime;
            throttle = -1f;
            steer = -steer;
            brake = 0f;
        }
        else if (throttle > 0.5f && car.SpeedKmh < 4f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.5f) { reverseTimer = 1f; stuckTimer = 0f; }
        }
        else stuckTimer = 0f;

        car.Throttle = throttle;
        car.Steer = steer;
        car.Brake = brake;
        car.UseNitro = nitro;

        // Spadla z dráhy nebo leží na střeše -> znovu kus za hráčem
        bool fell = transform.position.y < plan.points[index].y - 6f;
        if (Vector3.Dot(transform.up, Vector3.up) < 0.3f) flipTimer += Time.deltaTime; else flipTimer = 0f;
        if (fell || flipTimer > 2.5f) RespawnBehind(targetIndex, 25);
    }

    /// <summary>Objeví se na dráze daný počet bodů za hráčem (na bezpečném místě).</summary>
    public void RespawnBehind(int playerIndex, int samplesBack)
    {
        int i = plan.SafeBefore(Mathf.Max(plan.policeStart, playerIndex - samplesBack));
        Vector3 p = plan.points[i] + plan.Right(i) * UnityEngine.Random.Range(-2.5f, 2.5f) + Vector3.up * 0.8f;
        car.Teleport(p, Quaternion.LookRotation(plan.Forward(i), Vector3.up));
        index = i;
        flipTimer = 0f;
        stuckTimer = 0f;
    }
}
