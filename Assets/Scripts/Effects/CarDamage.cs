using UnityEngine;

/// <summary>
/// Poškození auta: při nárazu se karoserie v místě nárazu promáčkne, odletí úlomky laku,
/// a při velkém poškození se z kapoty kouří a auto ztrácí maximální rychlost.
/// Úroveň nastavuje GameSettings.Damage (0 vypnuto, 1 jen vzhled, 2 i jízdní vlastnosti).
/// </summary>
public class CarDamage : MonoBehaviour
{
    public Material debrisMaterial;
    public int level = 2;

    /// <summary>0 = nové auto, 1 = vrak.</summary>
    public float Damage { get; private set; }

    private CarController car;
    private Mesh mesh;
    private Vector3[] vertices;
    private Transform body;
    private float baseTopSpeed;
    private float smokeTimer;
    private static Mesh debrisMesh;

    private void Start()
    {
        car = GetComponent<CarController>();
        baseTopSpeed = car.maxSpeedKmh;
        body = transform.Find("Body");
        if (body != null)
        {
            var filter = body.GetComponent<MeshFilter>();
            mesh = Instantiate(filter.sharedMesh);   // vlastní kopie - deformace nesmí ovlivnit jiná auta
            filter.sharedMesh = mesh;
            vertices = mesh.vertices;
        }
        car.Impact += OnImpact;
    }

    private void OnDestroy()
    {
        if (car != null) car.Impact -= OnImpact;
    }

    private void OnImpact(float strength)
    {
        if (level <= 0 || strength < 4f) return;
        float amount = Mathf.Clamp01((strength - 4f) / 25f);
        Damage = Mathf.Clamp01(Damage + amount * 0.25f);

        Deform(car.LastImpactPoint, car.LastImpactNormal, amount);
        SpawnDebris(car.LastImpactPoint, Mathf.RoundToInt(2 + amount * 6f));

        if (level >= 2)
            car.maxSpeedKmh = baseTopSpeed * (1f - Damage * 0.15f);
    }

    private void Deform(Vector3 worldPoint, Vector3 worldNormal, float amount)
    {
        if (mesh == null || body == null) return;
        Vector3 local = body.InverseTransformPoint(worldPoint);
        Vector3 push = body.InverseTransformDirection(worldNormal).normalized * (0.06f + amount * 0.18f);
        const float radius = 0.9f;
        bool changed = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            float d = Vector3.Distance(vertices[i], local);
            if (d > radius) continue;
            float falloff = 1f - d / radius;
            vertices[i] += push * falloff * falloff;
            changed = true;
        }
        if (!changed) return;
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    private void SpawnDebris(Vector3 point, int count)
    {
        if (debrisMaterial == null) return;
        if (debrisMesh == null)
        {
            var mb = new MeshBuilder(1);
            mb.Box(0, Vector3.zero, new Vector3(0.18f, 0.04f, 0.14f));
            debrisMesh = mb.ToMesh("Debris");
        }
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Debris");
            go.layer = 2;
            go.transform.SetPositionAndRotation(point + Random.insideUnitSphere * 0.3f, Random.rotation);
            go.AddComponent<MeshFilter>().sharedMesh = debrisMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = debrisMaterial;
            go.AddComponent<BoxCollider>().size = new Vector3(0.18f, 0.04f, 0.14f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.5f;
            rb.velocity = car.Body.velocity * 0.5f + (Vector3.up + Random.insideUnitSphere) * Random.Range(2f, 5f);
            rb.angularVelocity = Random.insideUnitSphere * 10f;
            Destroy(go, 6f);
        }
    }

    private void Update()
    {
        if (Damage < 0.6f || ParticleFX.Instance == null || PauseMenu.GameIsPaused) return;
        smokeTimer -= Time.deltaTime;
        if (smokeTimer > 0f) return;
        smokeTimer = Mathf.Lerp(0.25f, 0.06f, (Damage - 0.6f) / 0.4f);
        // Kouř z motoru (kapota vepředu)
        ParticleFX.Instance.Smoke(transform.TransformPoint(0f, 1.0f, 1.3f), car.Body.velocity);
    }
}
