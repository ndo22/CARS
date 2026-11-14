using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Zničitelný objekt: díl svodidla, stoh pneumatik, prkno zátarasu nebo bedna.
///
/// Dokud se nic nestane, je objekt kinematický (stojí pevně a nic nestojí výkon). Když do něj auto narazí
/// dost prudce (rychlost kolmo na objekt nad <see cref="breakSpeed"/>), utrhne se: začne padat a odletí,
/// auto si přitom zachová většinu rychlosti (proráží, nezastaví se o zeď). Slabší nárazy se sčítají
/// a díl se postupně naklání, takže se dá utrhnout i opakovaným otíráním.
/// Utržený díl na silnici se po chvíli propadne do země, mimo silnici zůstane ležet déle.
/// </summary>
public class Breakable : MonoBehaviour
{
    public enum Kind
    {
        Barrier,  // díl svodidla - láme se při nárazu kolmo na svodidlo
        Loose,    // stoh pneumatik, kužel - odletí při jakémkoli nárazu
        Plank,    // prkno zátarasu - celý zátaras se rozletí najednou
        Crate     // bedna - rozpadne se na prkýnka
    }

    public Kind kind = Kind.Barrier;
    [Tooltip("Rychlost nárazu (m/s) kolmo na objekt, od které se utrhne.")]
    public float breakSpeed = 8f;
    [Tooltip("Kolik rychlosti si auto po proražení zachová (0..1).")]
    public float keepMomentum = 0.7f;
    [Tooltip("Hmotnost po utržení (kg).")]
    public float mass = 45f;
    [Tooltip("Osa v prostoru objektu, kolmá na jeho čelo (u svodidla směr ke trati).")]
    public Vector3 localNormal = Vector3.right;
    [Tooltip("Za kolik sekund se utržený díl na silnici propadne do země.")]
    public float onRoadLifetime = 4f;
    [Tooltip("Za kolik sekund se propadne díl, který skončil mimo silnici.")]
    public float offRoadLifetime = 25f;
    [Tooltip("Díly, které se utrhnou spolu s tímto (prkna a podstavce jednoho zátarasu).")]
    public Breakable[] group;

    /// <summary>Zda je bod na silnici (nastaví scéna podle trati). Bez něj se díly propadnou vždy po <see cref="onRoadLifetime"/>.</summary>
    public static Func<Vector3, bool> IsOnRoad;
    /// <summary>Auto prorazilo objekt.</summary>
    public static event Action<CarController, Breakable> Broken;
    /// <summary>Místo a směr nárazu a počet jisker (pro <see cref="ParticleFX"/>).</summary>
    public static event Action<Vector3, Vector3, int> Smashed;

    private static Mesh splinterMesh;

    public bool IsBroken { get; private set; }

    private Rigidbody body;
    private float damage;
    private float tilt;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.mass = mass;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsBroken) return;
        Rigidbody other = collision.rigidbody;
        if (other == null) return;
        var car = other.GetComponent<CarController>();
        if (car == null) return;

        // Rychlost auta vůči objektu před nárazem (znaménko relativeVelocity se liší podle strany - otočíme ji k objektu)
        Vector3 hit = collision.relativeVelocity;
        if (Vector3.Dot(hit, transform.position - other.position) < 0f) hit = -hit;

        float impact = kind == Kind.Barrier || kind == Kind.Plank
            ? Mathf.Abs(Vector3.Dot(hit, transform.TransformDirection(localNormal).normalized))
            : hit.magnitude;
        if (impact < 2f) return;
        damage += impact;

        if (impact >= breakSpeed || damage >= breakSpeed * 2.2f)
        {
            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            Break(car, hit, point);
        }
        else if (kind == Kind.Barrier)
        {
            // Slabší náraz - díl se trochu nakloní pryč od auta (vidět, že už je "nalomený")
            float add = impact / breakSpeed * 4f;
            if (tilt + add > 10f) add = Mathf.Max(0f, 10f - tilt);
            tilt += add;
            float side = Mathf.Sign(Vector3.Dot(hit, transform.right));
            transform.RotateAround(transform.position - transform.up * transform.lossyScale.y * 0.5f, transform.forward, -side * add);
        }
    }

    /// <summary>Utrhne objekt (a jeho skupinu). Auto <paramref name="car"/> projede s většinou rychlosti.</summary>
    public void Break(CarController car, Vector3 hitVelocity, Vector3 point)
    {
        if (IsBroken) return;
        bool wasSolid = body.isKinematic;
        Broken?.Invoke(car, this);
        if (car != null && !(car is AICar)) Achievements.ObjectSmashed();

        // Auto proráží: vrátíme mu část rychlosti, kterou mu kinematický objekt při nárazu vzal
        if (wasSolid && car != null && car.Body != null)
        {
            Rigidbody cb = car.Body;
            Vector3 v = cb.velocity;
            Vector3 keep = new Vector3(hitVelocity.x, 0f, hitVelocity.z) * keepMomentum;
            if (new Vector3(v.x, 0f, v.z).sqrMagnitude < keep.sqrMagnitude)
                cb.velocity = new Vector3(keep.x, v.y, keep.z);
            cb.angularVelocity *= 0.5f;
        }

        Smashed?.Invoke(point, -hitVelocity.normalized, kind == Kind.Barrier ? 10 : 4);

        Detach(hitVelocity, 1f);
        if (group != null)
            foreach (Breakable b in group)
                if (b != null && b != this) b.Detach(hitVelocity, Random.Range(0.5f, 0.8f));
    }

    private void Detach(Vector3 hitVelocity, float share)
    {
        if (IsBroken) return;
        IsBroken = true;
        if (kind == Kind.Crate)
        {
            Shatter(hitVelocity);
            return;
        }
        body.isKinematic = false;
        body.mass = mass;
        body.drag = 0.15f;
        body.angularDrag = 0.6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.velocity = hitVelocity * Random.Range(0.5f, 0.85f) * share + Vector3.up * Random.Range(1.5f, 4f);
        body.angularVelocity = Random.insideUnitSphere * 5f;
        StartCoroutine(SinkLater());
    }

    private IEnumerator SinkLater()
    {
        float t = 0f;
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            t += 0.5f;
            bool onRoad = IsOnRoad == null || IsOnRoad(transform.position);
            if (t >= (onRoad ? onRoadLifetime : offRoadLifetime)) break;
        }
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        body.isKinematic = true;
        Vector3 start = transform.position;
        for (float s = 0f; s < 1.5f; s += Time.deltaTime)
        {
            transform.position = start + Vector3.down * (s * 1.4f);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Bedna se rozpadne na prkýnka.</summary>
    private void Shatter(Vector3 hitVelocity)
    {
        if (splinterMesh == null)
        {
            var mb = new MeshBuilder(1);
            mb.Box(0, Vector3.zero, new Vector3(1.2f, 0.12f, 0.3f));
            splinterMesh = mb.ToMesh("Splinter");
        }
        var renderer = GetComponent<MeshRenderer>();
        Material mat = renderer != null ? renderer.sharedMaterial : null;
        Vector3 size = transform.lossyScale;
        for (int i = 0; i < 7; i++)
        {
            var go = new GameObject("Splinter");
            go.layer = 2;
            go.transform.SetPositionAndRotation(transform.position + Vector3.Scale(Random.insideUnitSphere * 0.5f, size), Random.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = splinterMesh;
            if (mat != null) go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<BoxCollider>().size = new Vector3(1.2f, 0.12f, 0.3f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 2f;
            rb.velocity = hitVelocity * Random.Range(0.4f, 0.9f) + (Vector3.up * 1.5f + Random.insideUnitSphere) * Random.Range(2f, 4.5f);
            rb.angularVelocity = Random.insideUnitSphere * 12f;
            Destroy(go, Random.Range(3.5f, 5f));
        }
        Destroy(gameObject);
    }

    /// <summary>Vytvoří zničitelný objekt se sdíleným meshem a box colliderem.</summary>
    public static Breakable Create(string name, Transform parent, Mesh mesh, Material[] materials, Vector3 position, Quaternion rotation,
        Vector3 scale, Vector3 colliderSize, Vector3 colliderCenter, Kind kind)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        var box = go.AddComponent<BoxCollider>();
        box.size = colliderSize;
        box.center = colliderCenter;
        var b = go.AddComponent<Breakable>();
        b.kind = kind;
        return b;
    }
}
