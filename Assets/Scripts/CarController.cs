using UnityEngine;

/// <summary>
/// Společná fyzika auta pro hráče (<see cref="Car"/>) i počítač (<see cref="AICar"/>).
/// Potomek jen nastaví vstupy (plyn, zatáčení, brzda, ruční brzda) a o zbytek se postará tahle třída:
/// pohon zadních kol, brzdy, omezení rychlosti, zatáčení citlivé na rychlost, přítlak a stabilizátory.
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

    /// <summary>Když je false (odpočet, konec závodu), auto stojí na brzdě.</summary>
    public bool InputEnabled { get; set; } = true;

    public float SpeedKmh => _rigidbody != null ? _rigidbody.velocity.magnitude * 3.6f : 0f;
    public float ForwardSpeedKmh => _rigidbody != null ? Vector3.Dot(_rigidbody.velocity, transform.forward) * 3.6f : 0f;
    public Rigidbody Body => _rigidbody;

    // Vstupy nastavované potomkem
    protected float throttleInput;   // -1 (couvání) .. 1 (plyn)
    protected float steerInput;      // -1 .. 1
    protected float brakeInput;      // 0 .. 1
    protected bool handbrakeInput;

    private Rigidbody _rigidbody;

    protected virtual void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (centerOfMass != null)
            _rigidbody.centerOfMass = centerOfMass.localPosition;
        // Plynulejší pohyb auta pro kameru
        _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    protected virtual void FixedUpdate()
    {
        float throttle = throttleInput;
        float brake = brakeInput;
        bool handbrake = handbrakeInput;

        if (!InputEnabled)
        {
            throttle = 0f;
            brake = 1f;
            handbrake = true;
        }

        float speed = SpeedKmh;
        float forwardSpeed = ForwardSpeedKmh;

        // Omezovač rychlosti
        if (throttle > 0f && forwardSpeed > maxSpeedKmh) throttle = 0f;
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

        ApplyAntiRoll(wheelColliderLeftFront, wheelColliderRightFront);
        ApplyAntiRoll(wheelColliderLeftBack, wheelColliderRightBack);

        if (wheelColliderLeftBack.isGrounded || wheelColliderRightBack.isGrounded)
            _rigidbody.AddForce(-transform.up * downforce * _rigidbody.velocity.magnitude);
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
