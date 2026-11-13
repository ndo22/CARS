using System;
using UnityEngine;

/// <summary>
/// Společná fyzika auta pro hráče (<see cref="Car"/>) i počítač (<see cref="AICar"/>).
/// Potomek jen nastaví vstupy (plyn, zatáčení, brzda, ruční brzda, nitro) a o zbytek se postará tahle třída:
/// pohon zadních kol, brzdy, omezení rychlosti, zatáčení citlivé na rychlost, přítlak, stabilizátory,
/// drift (ruční brzda povolí zadní kola), nitro (nabíjí se driftováním) a přilnavost podle počasí.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class CarController : MonoBehaviour
{
    public Transform centerOfMass;

    public WheelCollider wheelColliderLeftFront;
    public WheelCollider wheelColliderRightFront;
    public WheelCollider wheelColliderLeftBack;
    public WheelCollider wheelColliderRightBack;

    public Transform LeftFront;
    public Transform RightFront;
    public Transform LeftBack;
    public Transform RightBack;

    [Header("Motor a řízení")]
    public float motorTorque = 150f;
    public float maxSteer = 15f;
    [Tooltip("Maximální rychlost vpřed v km/h.")]
    public float maxSpeedKmh = 200f;
    [Tooltip("Maximální rychlost couvání v km/h.")]
    public float maxReverseSpeedKmh = 30f;
    [Tooltip("Kolik z maximálního natočení kol zbude při maximální rychlosti (0-1). Menší číslo = stabilnější auto v rychlosti.")]
    [Range(0.1f, 1f)] public float highSpeedSteerFactor = 0.45f;

    [Header("Brzdy")]
    public float brakeTorque = 900f;
    public float handbrakeTorque = 2000f;

    [Header("Stabilita")]
    [Tooltip("Přítlak rostoucí s rychlostí - auto lépe drží na trati.")]
    public float downforce = 40f;
    [Tooltip("Síla stabilizátoru - menší naklánění v zatáčkách a méně převrácení.")]
    public float antiRoll = 3000f;

    [Header("Přilnavost a drift")]
    [Tooltip("Násobič přilnavosti všech kol (déšť ~0.78).")]
    public float gripMultiplier = 1f;
    [Tooltip("Boční přilnavost zadních kol při ruční brzdě - menší = snazší drift.")]
    [Range(0.2f, 1f)] public float driftRearGrip = 0.55f;

    [Header("Nitro")]
    public float nitroForce = 3500f;
    [Tooltip("O kolik se při nitru zvýší maximální rychlost (km/h).")]
    public float nitroTopSpeedBonus = 35f;
    public float nitroDrainPerSecond = 0.35f;
    [Tooltip("Nabíjení nitra za sekundu driftu.")]
    public float nitroChargeFromDrift = 0.3f;
    [Tooltip("Pomalé nabíjení nitra za sekundu (i bez driftu).")]
    public float nitroPassiveCharge = 0.015f;

    /// <summary>Když je false (odpočet, konec závodu), auto stojí na brzdě.</summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>Zásoba nitra 0..1.</summary>
    public float Nitro { get; set; } = 0.35f;
    public bool NitroActive { get; private set; }
    /// <summary>Jak moc auto právě driftuje (0 = vůbec, 1 = hodně).</summary>
    public float DriftAmount { get; private set; }

    /// <summary>Odpor povrchu (tráva, písek) - nastavuje <see cref="CarEffects"/> podle toho, kde kola jedou.</summary>
    public float SurfaceDrag { get; set; }

    /// <summary>Náraz do něčeho - parametr je rychlost nárazu v m/s.</summary>
    public event Action<float> Impact;

    public float SpeedKmh => _rigidbody != null ? _rigidbody.velocity.magnitude * 3.6f : 0f;
    public float ForwardSpeedKmh => _rigidbody != null ? Vector3.Dot(_rigidbody.velocity, transform.forward) * 3.6f : 0f;
    public Rigidbody Body => _rigidbody;

    // Vstupy nastavované potomkem
    protected float throttleInput;   // -1 (couvání) .. 1 (plyn)
    protected float steerInput;      // -1 .. 1
    protected float brakeInput;      // 0 .. 1
    protected bool handbrakeInput;
    protected bool nitroInput;

    private Rigidbody _rigidbody;
    private WheelCollider[] wheels;
    private float[] baseForwardStiffness;
    private float[] baseSidewaysStiffness;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (centerOfMass != null)
            _rigidbody.centerOfMass = centerOfMass.localPosition;
        // Plynulejší pohyb auta pro kameru
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        wheels = new[] { wheelColliderLeftFront, wheelColliderRightFront, wheelColliderLeftBack, wheelColliderRightBack };
        baseForwardStiffness = new float[4];
        baseSidewaysStiffness = new float[4];
        for (int i = 0; i < 4; i++)
        {
            if (wheels[i] == null) continue;
            baseForwardStiffness[i] = wheels[i].forwardFriction.stiffness;
            baseSidewaysStiffness[i] = wheels[i].sidewaysFriction.stiffness;
        }
    }

    protected virtual void FixedUpdate()
    {
        float throttle = throttleInput;
        float brake = brakeInput;
        bool handbrake = handbrakeInput;
        bool nitro = nitroInput;

        if (!InputEnabled)
        {
            throttle = 0f;
            brake = 1f;
            handbrake = true;
            nitro = false;
        }

        float speed = SpeedKmh;
        float forwardSpeed = ForwardSpeedKmh;

        // Nitro
        NitroActive = nitro && Nitro > 0.01f && forwardSpeed > 5f;
        float topSpeed = maxSpeedKmh + (NitroActive ? nitroTopSpeedBonus : 0f);
        if (NitroActive)
        {
            Nitro = Mathf.Max(0f, Nitro - nitroDrainPerSecond * Time.fixedDeltaTime);
            if (forwardSpeed < topSpeed && IsGrounded())
                _rigidbody.AddForce(transform.forward * nitroForce);
            throttle = Mathf.Max(throttle, 1f);
        }

        // Omezovač rychlosti
        if (throttle > 0f && forwardSpeed > topSpeed) throttle = 0f;
        if (throttle < 0f && forwardSpeed < -maxReverseSpeedKmh) throttle = 0f;

        float torque = throttle * motorTorque;
        wheelColliderLeftBack.motorTorque = torque;
        wheelColliderRightBack.motorTorque = torque;

        float frontBrake = brake * brakeTorque;
        float rearBrake = handbrake ? handbrakeTorque : brake * brakeTorque * 0.7f;
        wheelColliderLeftFront.brakeTorque = frontBrake;
        wheelColliderRightFront.brakeTorque = frontBrake;
        wheelColliderLeftBack.brakeTorque = rearBrake;
        wheelColliderRightBack.brakeTorque = rearBrake;

        // Čím rychleji, tím menší natočení kol -> auto se v rychlosti nerozhází
        float speedFactor = Mathf.Clamp01(speed / Mathf.Max(1f, maxSpeedKmh));
        float steerAngle = steerInput * maxSteer * Mathf.Lerp(1f, highSpeedSteerFactor, speedFactor);
        wheelColliderLeftFront.steerAngle = steerAngle;
        wheelColliderRightFront.steerAngle = steerAngle;

        ApplyGrip(handbrake && InputEnabled && forwardSpeed > 20f);
        UpdateDrift(speed);

        ApplyAntiRoll(wheelColliderLeftFront, wheelColliderRightFront);
        ApplyAntiRoll(wheelColliderLeftBack, wheelColliderRightBack);

        if (wheelColliderLeftBack.isGrounded || wheelColliderRightBack.isGrounded)
            _rigidbody.AddForce(-transform.up * downforce * _rigidbody.velocity.magnitude);

        // Mimo silnici auto brzdí tráva/písek
        if (SurfaceDrag > 0f)
        {
            Vector3 flat = Vector3.ProjectOnPlane(_rigidbody.velocity, transform.up);
            _rigidbody.AddForce(-flat * SurfaceDrag, ForceMode.Acceleration);
        }
    }

    private bool IsGrounded()
    {
        return wheelColliderLeftBack.isGrounded || wheelColliderRightBack.isGrounded;
    }

    private void ApplyGrip(bool drifting)
    {
        for (int i = 0; i < 4; i++)
        {
            WheelCollider w = wheels[i];
            if (w == null) continue;
            WheelFrictionCurve f = w.forwardFriction;
            f.stiffness = baseForwardStiffness[i] * gripMultiplier;
            w.forwardFriction = f;

            WheelFrictionCurve s = w.sidewaysFriction;
            bool rear = i >= 2;
            s.stiffness = baseSidewaysStiffness[i] * gripMultiplier * (rear && drifting ? driftRearGrip : 1f);
            w.sidewaysFriction = s;
        }
    }

    private void UpdateDrift(float speed)
    {
        float slip = 0f;
        for (int i = 2; i < 4; i++)
        {
            if (wheels[i] != null && wheels[i].GetGroundHit(out WheelHit hit))
                slip = Mathf.Max(slip, Mathf.Abs(hit.sidewaysSlip));
        }

        // Drift = auto jede bokem dostatečně rychle
        float sideways = _rigidbody.velocity.sqrMagnitude > 1f
            ? Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(_rigidbody.velocity, transform.up)) : 0f;
        float drift = speed > 35f ? Mathf.Clamp01((sideways - 8f) / 25f) * Mathf.Clamp01(slip / 0.3f) : 0f;
        DriftAmount = Mathf.Lerp(DriftAmount, drift, Time.fixedDeltaTime * 8f);

        if (!NitroActive)
            Nitro = Mathf.Min(1f, Nitro + (DriftAmount * nitroChargeFromDrift + nitroPassiveCharge) * Time.fixedDeltaTime);
    }

    private void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        if (antiRoll <= 0f) return;

        float travelLeft = 1f;
        float travelRight = 1f;

        bool groundedLeft = left.GetGroundHit(out WheelHit hitLeft);
        if (groundedLeft)
            travelLeft = (-left.transform.InverseTransformPoint(hitLeft.point).y - left.radius) / left.suspensionDistance;

        bool groundedRight = right.GetGroundHit(out WheelHit hitRight);
        if (groundedRight)
            travelRight = (-right.transform.InverseTransformPoint(hitRight.point).y - right.radius) / right.suspensionDistance;

        float force = (travelLeft - travelRight) * antiRoll;

        if (groundedLeft)
            _rigidbody.AddForceAtPosition(left.transform.up * -force, left.transform.position);
        if (groundedRight)
            _rigidbody.AddForceAtPosition(right.transform.up * force, right.transform.position);
    }

    /// <summary>Místo posledního nárazu (pro jiskry).</summary>
    public Vector3 LastImpactPoint { get; private set; }
    public Vector3 LastImpactNormal { get; private set; }

    private void OnCollisionEnter(Collision collision)
    {
        float strength = collision.relativeVelocity.magnitude;
        if (strength > 2f)
        {
            if (collision.contactCount > 0)
            {
                ContactPoint contact = collision.GetContact(0);
                LastImpactPoint = contact.point;
                LastImpactNormal = contact.normal;
            }
            Impact?.Invoke(strength);
        }
    }

    protected virtual void Update()
    {
        UpdateWheelVisual(wheelColliderLeftFront, LeftFront, false);
        UpdateWheelVisual(wheelColliderRightFront, RightFront, true);
        UpdateWheelVisual(wheelColliderLeftBack, LeftBack, false);
        UpdateWheelVisual(wheelColliderRightBack, RightBack, true);
    }

    private static void UpdateWheelVisual(WheelCollider wheel, Transform visual, bool flip)
    {
        if (wheel == null || visual == null) return;
        wheel.GetWorldPose(out Vector3 pos, out Quaternion rot);
        visual.position = pos;
        // Pravá kola má model otočený o 180°
        visual.rotation = flip ? rot * Quaternion.Euler(0, 180, 0) : rot;
    }

    /// <summary>Přesune auto na dané místo a zastaví ho (po havárii / převrácení).</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.position = position;
        _rigidbody.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);

        foreach (var wheel in new[] { wheelColliderLeftFront, wheelColliderRightFront, wheelColliderLeftBack, wheelColliderRightBack })
        {
            wheel.motorTorque = 0f;
            wheel.brakeTorque = brakeTorque;
        }
    }
}
