using UnityEngine;

/// <summary>
/// Sdílené částice: kouř z pneumatik, prach mimo silnici a jiskry při nárazu.
/// Částice jsou malé low-poly kostičky, které se zmenšují do ztracena (nepotřebují průhledný shader).
/// </summary>
public class ParticleFX : MonoBehaviour
{
    public static ParticleFX Instance { get; private set; }

    private ParticleSystem smoke, dust, sparks, splash;

    public static ParticleFX Create(Material smokeMat, Material dustMat, Material sparkMat, Material splashMat)
    {
        var go = new GameObject("ParticleFX");
        var fx = go.AddComponent<ParticleFX>();
        Instance = fx;
        Mesh cube = CubeMesh();
        fx.smoke = MakeSystem(go.transform, "Smoke", cube, smokeMat, 1.2f, 600);
        fx.dust = MakeSystem(go.transform, "Dust", cube, dustMat, 1.0f, 600);
        fx.sparks = MakeSystem(go.transform, "Sparks", cube, sparkMat, 0.5f, 200);
        fx.splash = MakeSystem(go.transform, "Splash", cube, splashMat, 0.5f, 400);
        return fx;
    }

    private static ParticleSystem MakeSystem(Transform parent, string name, Mesh mesh, Material material, float lifetime, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; // emise je vypnutá, částice vznikají jen přes Emit()
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = 0f;
        main.startSize = 0.4f;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = name == "Sparks" || name == "Splash" ? 1.2f : -0.05f;
        main.startRotation3D = true;

        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rotation.y = new ParticleSystem.MinMaxCurve(-2f, 2f);
        rotation.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        return ps;
    }

    private static Mesh CubeMesh()
    {
        var mb = new MeshBuilder(1);
        mb.Box(0, Vector3.zero, Vector3.one);
        return mb.ToMesh("ParticleCube");
    }

    private static void Emit(ParticleSystem ps, Vector3 position, Vector3 velocity, float size, float lifetime)
    {
        if (ps == null) return;
        var p = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = velocity,
            startSize = size,
            startLifetime = lifetime,
            rotation3D = new Vector3(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)),
            applyShapeToPosition = false,
        };
        ps.Emit(p, 1);
    }

    public void Smoke(Vector3 position, Vector3 carVelocity)
    {
        Emit(smoke, position + Random.insideUnitSphere * 0.2f, carVelocity * 0.2f + Vector3.up * Random.Range(0.6f, 1.4f) + Random.insideUnitSphere * 0.6f,
            Random.Range(0.35f, 0.7f), Random.Range(0.8f, 1.4f));
    }

    public void Dust(Vector3 position, Vector3 carVelocity)
    {
        Emit(dust, position + Random.insideUnitSphere * 0.3f, carVelocity * 0.15f + Vector3.up * Random.Range(0.5f, 1.5f) + Random.insideUnitSphere,
            Random.Range(0.3f, 0.65f), Random.Range(0.6f, 1.1f));
    }

    public void Sparks(Vector3 position, Vector3 normal, int count)
    {
        for (int i = 0; i < count; i++)
            Emit(sparks, position, (normal + Random.insideUnitSphere).normalized * Random.Range(3f, 7f), Random.Range(0.06f, 0.14f), Random.Range(0.3f, 0.6f));
    }

    public void Splash(Vector3 position, Vector3 carVelocity)
    {
        Emit(splash, position, carVelocity * 0.3f + Vector3.up * Random.Range(1f, 2.5f) + Random.insideUnitSphere * 0.8f,
            Random.Range(0.08f, 0.16f), Random.Range(0.3f, 0.5f));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
