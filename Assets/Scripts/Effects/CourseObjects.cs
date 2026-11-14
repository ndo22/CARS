using System;
using UnityEngine;

/// <summary>Cílová čára překážkové dráhy - ohlásí, které auto ji projelo.</summary>
public class FinishLine : MonoBehaviour
{
    public static event Action<CarController> Crossed;

    private void OnTriggerEnter(Collider other)
    {
        var car = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<CarController>() : null;
        if (car != null) Crossed?.Invoke(car);
    }
}

/// <summary>Boost pad: auto, které přejede, dostane prudké zrychlení a trochu nitra.</summary>
public class BoostPad : MonoBehaviour
{
    public float strength = 9f;
    public static event Action<CarController> Boosted;

    private void OnTriggerEnter(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;
        var car = rb.GetComponent<CarController>();
        if (car == null) return;
        Vector3 forward = transform.forward;
        float along = Vector3.Dot(rb.velocity, forward);
        // Zrychlit ve směru padu, ale ne nad rozumnou mez
        float add = Mathf.Clamp(strength, 0f, Mathf.Max(0f, 45f - along));
        rb.AddForce(forward * add, ForceMode.VelocityChange);
        car.Nitro = Mathf.Min(1f, car.Nitro + 0.3f);
        Boosted?.Invoke(car);
    }
}

/// <summary>Kmitá objektem tam a zpět (posuvné zátarasy). Pro kinematický rigidbody používá MovePosition.</summary>
public class Oscillator : MonoBehaviour
{
    public Vector3 axis = Vector3.right;
    public float amplitude = 4f;
    public float speed = 1f;
    public float phase;

    private Vector3 origin;
    private Rigidbody body;

    private void Start()
    {
        origin = transform.position;
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 p = origin + axis.normalized * Mathf.Sin(Time.time * speed + phase) * amplitude;
        if (body != null) body.MovePosition(p);
        else transform.position = p;
    }
}

