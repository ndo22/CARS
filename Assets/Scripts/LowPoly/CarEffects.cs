using UnityEngine;

/// <summary>
/// Efekty auta: stopy smyku, kouř z pneumatik, prach a zpomalení mimo silnici, cákání v dešti,
/// plameny nitra, pískání pneumatik, zvuk nitra a nárazů (s jiskrami).
/// </summary>
public class CarEffects : MonoBehaviour
{
    public float sidewaysSlipThreshold = 0.35f;
    public float forwardSlipThreshold = 0.7f;
    [Tooltip("Zpomalení na trávě / písku (m/s² na každý m/s rychlosti).")]
    public float offRoadDrag = 0.12f;
    [Tooltip("3D zvuk (soupeři) nebo 2D (hráč).")]
    public bool spatialAudio;
    public float volume = 1f;
    public bool rain;

    private CarController car;
    private WheelCollider[] wheels;
    private Vector3[] lastPoint;
    private bool[] hadMark;
    private GameObject flame;
    private AudioSource squealSource, nitroSource, impactSource;
    private float particleTimer;

    private void Start()
    {
        car = GetComponent<CarController>();
        wheels = new[] { car.wheelColliderLeftBack, car.wheelColliderRightBack, car.wheelColliderLeftFront, car.wheelColliderRightFront };
        lastPoint = new Vector3[wheels.Length];
        hadMark = new bool[wheels.Length];
        Transform f = transform.Find("NitroFlame");
        flame = f != null ? f.gameObject : null;

        squealSource = MakeSource(ProceduralAudio.Squeal(), true);
        nitroSource = MakeSource(ProceduralAudio.Nitro(), true);
        impactSource = MakeSource(ProceduralAudio.Impact(), false);
        car.Impact += OnImpact;
    }

    private void OnDestroy()
    {
        if (car != null) car.Impact -= OnImpact;
    }

    private AudioSource MakeSource(AudioClip clip, bool loop)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.volume = 0f;
        source.spatialBlend = spatialAudio ? 1f : 0f;
        source.maxDistance = 90f;
        source.rolloffMode = AudioRolloffMode.Linear;
        if (loop) source.Play();
        return source;
    }

    private void OnImpact(float strength)
    {
        float sfx = GameSettings.Sfx * volume;
        impactSource.pitch = Random.Range(0.85f, 1.15f);
        impactSource.PlayOneShot(impactSource.clip, Mathf.Clamp01(strength / 15f) * sfx);
        if (ParticleFX.Instance != null && strength > 5f)
            ParticleFX.Instance.Sparks(car.LastImpactPoint, car.LastImpactNormal, Mathf.Clamp((int)strength, 4, 20));
        if (strength > 4f)
            ChaseCameraFX.Shake(transform, Mathf.Clamp01(strength / 20f));
    }

    private void Update()
    {
        if (car == null) return;
        bool paused = PauseMenu.GameIsPaused;

        if (flame != null)
        {
            flame.SetActive(car.NitroActive && !paused);
            if (car.NitroActive)
                flame.transform.localScale = new Vector3(1f, 1f, Random.Range(0.7f, 1.3f));
        }

        float sfx = paused ? 0f : GameSettings.Sfx * volume;
        nitroSource.volume = Mathf.MoveTowards(nitroSource.volume, car.NitroActive ? 0.55f * sfx : 0f, Time.unscaledDeltaTime * 4f);
    }

    private void FixedUpdate()
    {
        if (car == null) return;

        float maxSlip = 0f;
        bool offRoad = false;
        particleTimer += Time.fixedDeltaTime;
        bool emit = particleTimer > 0.04f;
        if (emit) particleTimer = 0f;
        Vector3 velocity = car.Body.velocity;

        for (int i = 0; i < wheels.Length; i++)
        {
            WheelCollider wheel = wheels[i];
            bool skid = false;
            if (wheel != null && wheel.GetGroundHit(out WheelHit hit))
            {
                bool grass = SurfaceType.Of(hit.collider) == SurfaceType.Kind.Grass;
                offRoad |= grass;

                float slip = Mathf.Max(Mathf.Abs(hit.sidewaysSlip) / sidewaysSlipThreshold, Mathf.Abs(hit.forwardSlip) / forwardSlipThreshold);
                if (!grass && car.SpeedKmh > 8f) maxSlip = Mathf.Max(maxSlip, slip);
                skid = !grass && car.SpeedKmh > 8f && slip > 1f;

                if (skid)
                {
                    if (SkidMarks.Instance != null)
                    {
                        if (hadMark[i] && (hit.point - lastPoint[i]).sqrMagnitude > 0.09f)
                        {
                            SkidMarks.Instance.AddSegment(lastPoint[i], hit.point, hit.normal);
                            lastPoint[i] = hit.point;
                        }
                        else if (!hadMark[i])
                            lastPoint[i] = hit.point;
                    }
                    if (emit && ParticleFX.Instance != null && i < 2 && !rain)
                        ParticleFX.Instance.Smoke(hit.point + Vector3.up * 0.2f, velocity);
                }

                if (emit && ParticleFX.Instance != null && i < 2)
                {
                    if (grass && car.SpeedKmh > 15f)
                        ParticleFX.Instance.Dust(hit.point + Vector3.up * 0.15f, velocity);
                    else if (rain && !grass && car.SpeedKmh > 30f)
                        ParticleFX.Instance.Splash(hit.point + Vector3.up * 0.1f - transform.forward * 0.3f, velocity);
                }
            }
            hadMark[i] = skid;
        }

        car.SurfaceDrag = offRoad ? offRoadDrag : 0f;

        float sfx = PauseMenu.GameIsPaused ? 0f : GameSettings.Sfx * volume;
        float target = Mathf.Clamp01((maxSlip - 0.9f) * 1.2f) * 0.45f * sfx;
        squealSource.volume = Mathf.MoveTowards(squealSource.volume, target, Time.fixedDeltaTime * 3f);
        squealSource.pitch = 0.9f + Mathf.Clamp01(car.SpeedKmh / 150f) * 0.25f;
    }
}
