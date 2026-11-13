using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Postaví kompletní low-poly auto: karoserie z "řezů" (prstenců) podél délky auta,
/// prosklená kabina se sloupky, startovní čísla, světla, výfuky s plameny nitra, kola,
/// WheelCollidery, Rigidbody a skripty <see cref="Car"/>/<see cref="AICar"/>.
/// Každý styl má vlastní jízdní vlastnosti (<see cref="Stats"/>).
/// </summary>
public static class LowPolyCarFactory
{
    public enum BodyStyle { Sport, Muscle, Hatch, Pickup, Buggy, Formula }

    public static readonly string[] StyleNames = { "SPORT", "MUSCLE", "HATCH", "PICKUP", "BUGGY", "FORMULA" };

    /// <summary>Jízdní vlastnosti stylu.</summary>
    public struct CarStats
    {
        public float torque, topSpeed, steer, grip, mass;
        // Hodnoty 0..1 pro ukazatele v menu
        public float Speed => Mathf.InverseLerp(170f, 255f, topSpeed);
        public float Accel => Mathf.InverseLerp(0.5f, 1.05f, torque / mass);
        public float Grip => Mathf.InverseLerp(0.85f, 1.25f, grip);
        public float Handling => Mathf.InverseLerp(22f, 34f, steer);
    }

    public static CarStats Stats(BodyStyle style)
    {
        switch (style)
        {
            case BodyStyle.Muscle: return new CarStats { torque = 420f, topSpeed = 222f, steer = 25f, grip = 0.94f, mass = 540f };
            case BodyStyle.Hatch: return new CarStats { torque = 330f, topSpeed = 188f, steer = 32f, grip = 1.08f, mass = 460f };
            case BodyStyle.Pickup: return new CarStats { torque = 410f, topSpeed = 195f, steer = 26f, grip = 0.98f, mass = 600f };
            case BodyStyle.Buggy: return new CarStats { torque = 350f, topSpeed = 185f, steer = 33f, grip = 1.14f, mass = 440f };
            case BodyStyle.Formula: return new CarStats { torque = 450f, topSpeed = 250f, steer = 30f, grip = 1.22f, mass = 450f };
            default: return new CarStats { torque = 365f, topSpeed = 210f, steer = 28f, grip = 1.02f, mass = 500f };
        }
    }

    /// <summary>Jeden řez karoserií: poloha na délku, poloviční šířka, spodní a horní výška.</summary>
    private struct Station
    {
        public float z, halfWidth, bottom, top;
        public Station(float z, float halfWidth, float bottom, float top)
        {
            this.z = z; this.halfWidth = halfWidth; this.bottom = bottom; this.top = top;
        }
    }

    private class Shape
    {
        public Station[] body, cabin;
        public float front, rear;
        public float wheelRadius = 0.36f, wheelWidth = 0.3f;
        public bool openWheels;       // kola mimo karoserii (buggy, formule)
        public float openWheelX;
        public int[] cabinTops = { SubGlass, SubPaint, SubGlass };
    }

    private const int SubPaint = 0, SubGlass = 1, SubDark = 2, SubHeadlight = 3, SubTaillight = 4, SubTrim = 5;

    public class Palette
    {
        public Material paint, glass, dark, headlight, taillight, trim, tire, hub, flame;
    }

    /// <summary>Volby stavby.</summary>
    public class Options
    {
        public int number = -1;          // startovní číslo (-1 = žádné)
        public int pattern;              // vzor laku (Cosmetics.Patterns)
        public bool headlights;          // skutečná světla (noc)
        public float headlightIntensity = 2.2f;
    }

    // ================================================================ stavba

    public static GameObject Build<TController>(string name, Vector3 position, Quaternion rotation, BodyStyle style,
        Palette palette, int layer, out TController controller, Options options = null) where TController : CarController
    {
        options = options ?? new Options();
        // Vše skládáme v neaktivním objektu - Awake skriptů se zavolá až po nastavení všech odkazů
        var root = new GameObject(name);
        root.SetActive(false);
        root.layer = layer;
        root.transform.SetPositionAndRotation(position, rotation);

        Shape shape = GetShape(style);
        BuildBody(root.transform, style, shape, palette, layer, options);

        // --- fyzika
        CarStats stats = Stats(style);
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = stats.mass;
        rb.drag = 0.05f;
        rb.angularDrag = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Station[] body = shape.body;
        float length = body[body.Length - 1].z - body[0].z;
        float midZ = (body[body.Length - 1].z + body[0].z) * 0.5f;
        float wheelX = WheelX(shape);
        var lower = root.AddComponent<BoxCollider>();
        lower.center = new Vector3(0f, 0.72f, midZ);
        lower.size = new Vector3(Mathf.Max(wheelX * 2f + 0.25f, 1.2f), 0.6f, length);
        if (shape.cabin != null)
        {
            var upper = root.AddComponent<BoxCollider>();
            float cabMid = (shape.cabin[0].z + shape.cabin[shape.cabin.Length - 1].z) * 0.5f;
            upper.center = new Vector3(0f, 1.15f, cabMid);
            upper.size = new Vector3(wheelX * 2f - 0.2f, 0.45f, shape.cabin[shape.cabin.Length - 1].z - shape.cabin[0].z);
        }

        var com = new GameObject("CenterOfMass").transform;
        com.SetParent(root.transform, false);
        com.localPosition = new Vector3(0f, 0.4f, 0.05f);

        // --- kola
        float wheelY = 0.5f + (shape.wheelRadius - 0.36f);
        var lf = CreateWheel(root.transform, "LeftFront", new Vector3(-wheelX, wheelY, shape.front), shape, palette, layer, stats, out WheelCollider lfc);
        var rf = CreateWheel(root.transform, "RightFront", new Vector3(wheelX, wheelY, shape.front), shape, palette, layer, stats, out WheelCollider rfc);
        var lb = CreateWheel(root.transform, "LeftBack", new Vector3(-wheelX, wheelY, shape.rear), shape, palette, layer, stats, out WheelCollider lbc);
        var rb2 = CreateWheel(root.transform, "RightBack", new Vector3(wheelX, wheelY, shape.rear), shape, palette, layer, stats, out WheelCollider rbc);

        controller = root.AddComponent<TController>();
        controller.centerOfMass = com;
        controller.wheelColliderLeftFront = lfc;
        controller.wheelColliderRightFront = rfc;
        controller.wheelColliderLeftBack = lbc;
        controller.wheelColliderRightBack = rbc;
        controller.LeftFront = lf;
        controller.RightFront = rf;
        controller.LeftBack = lb;
        controller.RightBack = rb2;
        controller.motorTorque = stats.torque;
        controller.maxSteer = stats.steer;
        controller.maxSpeedKmh = stats.topSpeed;
        controller.gripMultiplier = 1f;

        return root;
    }

    /// <summary>Jen vzhled auta bez fyziky (ghost, dekorace).</summary>
    public static GameObject BuildVisual(string name, BodyStyle style, Palette palette, int layer, int number = -1)
    {
        var root = new GameObject(name);
        root.layer = layer;
        Shape shape = GetShape(style);
        BuildBody(root.transform, style, shape, palette, layer, new Options { number = number });
        float wheelX = WheelX(shape);
        float wheelY = 0.5f + (shape.wheelRadius - 0.36f) - 0.13f;
        foreach (var p in new[] { new Vector3(-wheelX, wheelY, shape.front), new Vector3(wheelX, wheelY, shape.front),
                                  new Vector3(-wheelX, wheelY, shape.rear), new Vector3(wheelX, wheelY, shape.rear) })
        {
            var w = new GameObject("Wheel");
            w.layer = layer;
            w.transform.SetParent(root.transform, false);
            w.transform.localPosition = p;
            w.AddComponent<MeshFilter>().sharedMesh = WheelMesh(shape);
            w.AddComponent<MeshRenderer>().sharedMaterials = new[] { palette.tire, palette.hub };
        }
        return root;
    }

    private static float WheelX(Shape shape)
    {
        if (shape.openWheels) return shape.openWheelX;
        // Vnější bok pneumatiky lícuje s karoserií, kolo je vidět v podběhu
        return Mathf.Min(Interpolate(shape.body, shape.front).halfWidth, Interpolate(shape.body, shape.rear).halfWidth) - shape.wheelWidth * 0.5f + 0.03f;
    }

    private static void BuildBody(Transform root, BodyStyle style, Shape shape, Palette palette, int layer, Options options)
    {
        var mb = new MeshBuilder(6);
        Station[] shell = shape.openWheels ? shape.body : WithWheelArches(shape.body, shape.front, shape.rear, shape.wheelRadius);
        Loft(mb, shell, 0.12f, SubPaint, null, SubDark);
        if (shape.cabin != null)
        {
            // Kabina: přední a zadní šikmá plocha jsou okna, prostředek je střecha
            Loft(mb, shape.cabin, 0.1f, SubGlass, shape.cabinTops, SubGlass);
            AddPillars(mb, shape.cabin);
        }
        AddDetails(mb, style, shape);
        if (options.number >= 0)
            AddNumbers(mb, style, shape, options.number, options.pattern != 3);
        if (options.pattern > 0)
            AddPattern(mb, shape, options.pattern);

        var bodyGo = new GameObject("Body");
        bodyGo.layer = layer;
        bodyGo.transform.SetParent(root, false);
        bodyGo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("CarBody");
        bodyGo.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            palette.paint, palette.glass, palette.dark, palette.headlight, palette.taillight, palette.trim
        };

        // Plameny z výfuků při nitru (zapíná je CarEffects)
        Station rear = shape.body[0];
        var flameMesh = new MeshBuilder(1);
        foreach (float side in new[] { -0.35f, 0.35f })
        {
            Vector3 b = new Vector3(side * (style == BodyStyle.Formula ? 0.4f : 1f), rear.bottom + 0.12f, rear.z - 0.05f);
            Cone(flameMesh, 0, b, Vector3.back, 0.12f, 0.9f);
        }
        if (palette.flame != null)
        {
            var flame = new GameObject("NitroFlame");
            flame.layer = layer;
            flame.transform.SetParent(root, false);
            flame.AddComponent<MeshFilter>().sharedMesh = flameMesh.ToMesh("Flame");
            var r = flame.AddComponent<MeshRenderer>();
            r.sharedMaterial = palette.flame;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flame.SetActive(false);
        }

        if (options.headlights)
        {
            Station front = shape.body[shape.body.Length - 1];
            var lightGo = new GameObject("Headlights");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, Mathf.Lerp(front.bottom, front.top, 0.6f), front.z + 0.2f);
            lightGo.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 45f;
            light.spotAngle = 70f;
            light.intensity = options.headlightIntensity;
            light.color = new Color(1f, 0.95f, 0.82f);
            light.shadows = LightShadows.None;
        }
    }

    // ================================================================ tvary

    private static Shape GetShape(BodyStyle style)
    {
        var s = new Shape();
        switch (style)
        {
            case BodyStyle.Muscle:
                s.body = new[]
                {
                    new Station(-2.25f, 0.88f, 0.4f, 0.95f), new Station(-2.05f, 0.98f, 0.32f, 1.0f),
                    new Station(-0.9f, 1.0f, 0.3f, 1.0f), new Station(1.3f, 1.0f, 0.3f, 0.98f),
                    new Station(2.1f, 0.98f, 0.32f, 0.88f), new Station(2.3f, 0.9f, 0.4f, 0.75f),
                };
                s.cabin = new[]
                {
                    new Station(-1.5f, 0.82f, 0.98f, 1.0f), new Station(-1.05f, 0.78f, 0.98f, 1.45f),
                    new Station(0.1f, 0.76f, 0.98f, 1.48f), new Station(0.75f, 0.84f, 0.96f, 1.0f),
                };
                s.front = 1.45f; s.rear = -1.4f;
                break;

            case BodyStyle.Hatch:
                s.body = new[]
                {
                    new Station(-1.75f, 0.85f, 0.4f, 1.0f), new Station(-1.6f, 0.92f, 0.32f, 1.05f),
                    new Station(1.0f, 0.92f, 0.32f, 0.95f), new Station(1.65f, 0.88f, 0.34f, 0.85f),
                    new Station(1.85f, 0.8f, 0.42f, 0.72f),
                };
                s.cabin = new[]
                {
                    new Station(-1.6f, 0.8f, 1.03f, 1.05f), new Station(-1.5f, 0.78f, 1.03f, 1.62f),
                    new Station(0.2f, 0.76f, 1.0f, 1.62f), new Station(1.0f, 0.84f, 0.94f, 0.96f),
                };
                s.front = 1.15f; s.rear = -1.15f;
                break;

            case BodyStyle.Pickup:
                s.body = new[]
                {
                    new Station(-2.35f, 0.9f, 0.45f, 1.0f), new Station(-2.25f, 0.98f, 0.4f, 1.05f),
                    new Station(-0.3f, 0.98f, 0.38f, 1.05f), new Station(1.5f, 0.98f, 0.38f, 1.08f),
                    new Station(2.15f, 0.95f, 0.4f, 0.98f), new Station(2.3f, 0.88f, 0.48f, 0.85f),
                };
                s.cabin = new[]
                {
                    new Station(-0.35f, 0.86f, 1.02f, 1.05f), new Station(-0.3f, 0.85f, 1.02f, 1.78f),
                    new Station(0.6f, 0.82f, 1.02f, 1.78f), new Station(1.2f, 0.9f, 1.0f, 1.06f),
                };
                s.cabinTops = new[] { SubPaint, SubPaint, SubGlass };
                s.front = 1.5f; s.rear = -1.55f; s.wheelRadius = 0.42f; s.wheelWidth = 0.34f;
                break;

            case BodyStyle.Buggy:
                s.body = new[]
                {
                    new Station(-1.6f, 0.72f, 0.5f, 0.85f), new Station(-1.4f, 0.8f, 0.45f, 0.98f),
                    new Station(0.5f, 0.78f, 0.45f, 0.92f), new Station(1.4f, 0.62f, 0.5f, 0.76f),
                    new Station(1.75f, 0.45f, 0.55f, 0.66f),
                };
                s.front = 1.25f; s.rear = -1.15f; s.wheelRadius = 0.44f; s.wheelWidth = 0.38f;
                s.openWheels = true; s.openWheelX = 1.02f;
                break;

            case BodyStyle.Formula:
                s.body = new[]
                {
                    new Station(-1.95f, 0.38f, 0.3f, 0.72f), new Station(-1.6f, 0.5f, 0.28f, 0.8f),
                    new Station(-0.9f, 0.48f, 0.28f, 0.86f), new Station(0.25f, 0.38f, 0.28f, 0.74f),
                    new Station(1.4f, 0.2f, 0.3f, 0.52f), new Station(2.25f, 0.12f, 0.33f, 0.42f),
                };
                s.front = 1.6f; s.rear = -1.45f; s.wheelRadius = 0.36f; s.wheelWidth = 0.4f;
                s.openWheels = true; s.openWheelX = 0.88f;
                break;

            default: // Sport
                s.body = new[]
                {
                    new Station(-2.1f, 0.85f, 0.42f, 0.88f), new Station(-1.95f, 0.98f, 0.3f, 0.95f),
                    new Station(-0.5f, 1.0f, 0.28f, 0.92f), new Station(1.1f, 0.98f, 0.28f, 0.83f),
                    new Station(1.95f, 0.9f, 0.3f, 0.66f), new Station(2.15f, 0.78f, 0.36f, 0.56f),
                };
                s.cabin = new[]
                {
                    new Station(-1.45f, 0.78f, 0.9f, 0.95f), new Station(-0.85f, 0.72f, 0.88f, 1.3f),
                    new Station(0.1f, 0.7f, 0.86f, 1.33f), new Station(0.95f, 0.8f, 0.8f, 0.84f),
                };
                s.front = 1.35f; s.rear = -1.3f;
                break;
        }
        return s;
    }

    // ================================================================ geometrie

    /// <summary>
    /// "Loft" - spojí řezy do uzavřeného tělesa. Každý řez je šestiúhelník se zkosenými horními hranami.
    /// sideSub = boky, topSubPerSegment = horní plochy pro každý úsek (null = stejné jako boky), bottomSub = podvozek.
    /// </summary>
    private static void Loft(MeshBuilder mb, Station[] st, float chamfer, int sideSub, int[] topSubPerSegment, int bottomSub)
    {
        Vector3[][] rings = new Vector3[st.Length][];
        for (int i = 0; i < st.Length; i++)
        {
            Station s = st[i];
            float c = Mathf.Min(chamfer, (s.top - s.bottom) * 0.4f);
            rings[i] = new[]
            {
                new Vector3(-s.halfWidth, s.bottom, s.z),
                new Vector3(-s.halfWidth, s.top - c, s.z),
                new Vector3(-s.halfWidth + c * 1.5f, s.top, s.z),
                new Vector3(s.halfWidth - c * 1.5f, s.top, s.z),
                new Vector3(s.halfWidth, s.top - c, s.z),
                new Vector3(s.halfWidth, s.bottom, s.z),
            };
        }

        for (int i = 0; i < st.Length - 1; i++)
        {
            int topSub = topSubPerSegment != null && i < topSubPerSegment.Length ? topSubPerSegment[i] : sideSub;
            int[] subs = { sideSub, topSub, topSub, topSub, sideSub, bottomSub };
            Vector3 segCenter = new Vector3(0f, (st[i].top + st[i].bottom + st[i + 1].top + st[i + 1].bottom) * 0.25f, (st[i].z + st[i + 1].z) * 0.5f);
            for (int k = 0; k < 6; k++)
            {
                int k2 = (k + 1) % 6;
                mb.QuadOutward(subs[k], rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k], segCenter);
            }
        }

        CapRing(mb, rings[0], sideSub, Vector3.back);
        CapRing(mb, rings[st.Length - 1], sideSub, Vector3.forward);
    }

    private static void CapRing(MeshBuilder mb, Vector3[] ring, int sub, Vector3 facing)
    {
        Vector3 c = Vector3.zero;
        foreach (var v in ring) c += v;
        c /= ring.Length;
        for (int k = 0; k < ring.Length; k++)
            mb.TriFacing(sub, c, ring[k], ring[(k + 1) % ring.Length], facing);
    }

    private static Station Interpolate(Station[] st, float z)
    {
        if (z <= st[0].z) return new Station(z, st[0].halfWidth, st[0].bottom, st[0].top);
        for (int i = 0; i < st.Length - 1; i++)
        {
            if (z <= st[i + 1].z)
            {
                float t = Mathf.InverseLerp(st[i].z, st[i + 1].z, z);
                return new Station(z,
                    Mathf.Lerp(st[i].halfWidth, st[i + 1].halfWidth, t),
                    Mathf.Lerp(st[i].bottom, st[i + 1].bottom, t),
                    Mathf.Lerp(st[i].top, st[i + 1].top, t));
            }
        }
        Station last = st[st.Length - 1];
        return new Station(z, last.halfWidth, last.bottom, last.top);
    }

    /// <summary>Přidá do profilu podběhy kol - nad koly se spodní hrana karoserie zvedne.</summary>
    private static Station[] WithWheelArches(Station[] st, float frontAxle, float rearAxle, float radius)
    {
        var zs = new List<float>();
        foreach (var s in st) zs.Add(s.z);
        foreach (float axle in new[] { frontAxle, rearAxle })
        {
            zs.Add(axle - radius - 0.14f);
            zs.Add(axle - radius * 0.7f);
            zs.Add(axle + radius * 0.7f);
            zs.Add(axle + radius + 0.14f);
        }
        zs.Sort();

        float archTop = 0.74f + (radius - 0.36f) * 2f;
        var result = new List<Station>();
        foreach (float z in zs)
        {
            if (result.Count > 0 && Mathf.Abs(result[result.Count - 1].z - z) < 0.02f) continue;
            Station s = Interpolate(st, z);
            foreach (float axle in new[] { frontAxle, rearAxle })
            {
                if (Mathf.Abs(z - axle) <= radius * 0.7f + 0.001f)
                    s.bottom = Mathf.Min(s.top - 0.1f, archTop);
            }
            result.Add(s);
        }
        return result.ToArray();
    }

    /// <summary>Trám mezi dvěma body (sloupky, ochranný rám).</summary>
    private static void Beam(MeshBuilder mb, int sub, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0001f) return;
        Vector3 up = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
        mb.Box(sub, (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), Quaternion.LookRotation(d, up));
    }

    private static void Cone(MeshBuilder mb, int sub, Vector3 baseCenter, Vector3 dir, float radius, float length)
    {
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        Vector3 tip = baseCenter + dir * length;
        const int sides = 6;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = baseCenter + rot * new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * radius;
            Vector3 p1 = baseCenter + rot * new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * radius;
            mb.TriOutward(sub, p0, p1, tip, baseCenter + dir * (length * 0.3f));
            mb.TriFacing(sub, baseCenter, p0, p1, -dir);
        }
    }

    /// <summary>Sloupky kabiny - A (u předního skla), B (uprostřed) a C (vzadu).</summary>
    private static void AddPillars(MeshBuilder mb, Station[] cabin)
    {
        Station back = cabin[0], roofBack = cabin[1], roofFront = cabin[2], front = cabin[3];
        const float t = 0.07f;
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 aBase = new Vector3(side * (front.halfWidth - 0.02f), front.top - 0.02f, front.z - 0.02f);
            Vector3 aTop = new Vector3(side * (roofFront.halfWidth - 0.06f), roofFront.top - 0.04f, roofFront.z);
            Beam(mb, SubPaint, aBase, aTop, t);
            Vector3 cBase = new Vector3(side * (back.halfWidth - 0.02f), back.top - 0.02f, back.z + 0.02f);
            Vector3 cTop = new Vector3(side * (roofBack.halfWidth - 0.06f), roofBack.top - 0.04f, roofBack.z);
            Beam(mb, SubPaint, cBase, cTop, t * 1.6f);
            float bz = (roofBack.z + roofFront.z) * 0.5f;
            float bw = (roofBack.halfWidth + roofFront.halfWidth) * 0.5f + 0.005f;
            Beam(mb, SubPaint, new Vector3(side * bw, roofFront.bottom + 0.02f, bz), new Vector3(side * (bw - 0.04f), roofFront.top - 0.05f, bz), t);
        }
    }

    private static void AddDetails(MeshBuilder mb, BodyStyle style, Shape shape)
    {
        Station[] body = shape.body;
        Station front = body[body.Length - 1];
        Station rear = body[0];

        // Světla
        float lightY = Mathf.Lerp(front.bottom, front.top, 0.6f);
        for (int side = -1; side <= 1; side += 2)
        {
            mb.Box(SubHeadlight, new Vector3(side * front.halfWidth * 0.65f, lightY, front.z + 0.01f), new Vector3(Mathf.Min(0.35f, front.halfWidth * 0.6f), 0.12f, 0.06f));
            mb.Box(SubTaillight, new Vector3(side * rear.halfWidth * 0.65f, Mathf.Lerp(rear.bottom, rear.top, 0.7f), rear.z - 0.01f), new Vector3(Mathf.Min(0.38f, rear.halfWidth * 0.6f), 0.12f, 0.06f));
            // Výfuky
            mb.Box(SubDark, new Vector3(side * 0.35f * (style == BodyStyle.Formula ? 0.4f : 1f), rear.bottom + 0.12f, rear.z - 0.02f), new Vector3(0.12f, 0.12f, 0.18f));
        }

        if (!shape.openWheels)
        {
            mb.Box(SubDark, new Vector3(0f, front.bottom + 0.06f, front.z + 0.02f), new Vector3(front.halfWidth * 2f, 0.14f, 0.1f));
            mb.Box(SubDark, new Vector3(0f, rear.bottom + 0.06f, rear.z - 0.02f), new Vector3(rear.halfWidth * 2f, 0.14f, 0.1f));
        }

        switch (style)
        {
            case BodyStyle.Sport:
            {
                float y = rear.top + 0.28f;
                mb.Box(SubPaint, new Vector3(0f, y, rear.z + 0.2f), new Vector3(1.7f, 0.06f, 0.35f));
                mb.Box(SubDark, new Vector3(-0.55f, y - 0.15f, rear.z + 0.22f), new Vector3(0.06f, 0.26f, 0.15f));
                mb.Box(SubDark, new Vector3(0.55f, y - 0.15f, rear.z + 0.22f), new Vector3(0.06f, 0.26f, 0.15f));
                break;
            }
            case BodyStyle.Muscle:
                mb.Box(SubDark, new Vector3(0f, 1.02f, 1.3f), new Vector3(0.5f, 0.1f, 0.8f));
                mb.Box(SubTrim, new Vector3(-0.25f, 1.005f, -0.35f), new Vector3(0.18f, 0.02f, 3.3f));
                mb.Box(SubTrim, new Vector3(0.25f, 1.005f, -0.35f), new Vector3(0.18f, 0.02f, 3.3f));
                break;
            case BodyStyle.Hatch:
                mb.Box(SubDark, new Vector3(-0.55f, 1.68f, -0.6f), new Vector3(0.06f, 0.06f, 1.6f));
                mb.Box(SubDark, new Vector3(0.55f, 1.68f, -0.6f), new Vector3(0.06f, 0.06f, 1.6f));
                break;
            case BodyStyle.Pickup:
            {
                // Korba: podlaha, boční stěny a zadní čelo
                mb.Box(SubDark, new Vector3(0f, 1.06f, -1.3f), new Vector3(1.72f, 0.04f, 1.85f));
                mb.Box(SubPaint, new Vector3(-0.92f, 1.24f, -1.3f), new Vector3(0.1f, 0.38f, 1.95f));
                mb.Box(SubPaint, new Vector3(0.92f, 1.24f, -1.3f), new Vector3(0.1f, 0.38f, 1.95f));
                mb.Box(SubPaint, new Vector3(0f, 1.24f, -2.27f), new Vector3(1.94f, 0.38f, 0.1f));
                mb.Box(SubDark, new Vector3(0f, 1.82f, 0.15f), new Vector3(1.4f, 0.08f, 0.2f)); // světla na střeše
                for (int i = -1; i <= 1; i++)
                    mb.Box(SubHeadlight, new Vector3(i * 0.4f, 1.82f, 0.26f), new Vector3(0.2f, 0.12f, 0.04f));
                break;
            }
            case BodyStyle.Buggy:
            {
                // Ochranný rám
                float x = 0.68f, yb = 0.95f, yt = 1.85f, zf = 0.55f, zr = -0.9f;
                Beam(mb, SubDark, new Vector3(-x, yb, zf), new Vector3(-x * 0.8f, yt, zf - 0.35f), 0.08f);
                Beam(mb, SubDark, new Vector3(x, yb, zf), new Vector3(x * 0.8f, yt, zf - 0.35f), 0.08f);
                Beam(mb, SubDark, new Vector3(-x, yb, zr), new Vector3(-x * 0.8f, yt, zr + 0.15f), 0.08f);
                Beam(mb, SubDark, new Vector3(x, yb, zr), new Vector3(x * 0.8f, yt, zr + 0.15f), 0.08f);
                Beam(mb, SubDark, new Vector3(-x * 0.8f, yt, zf - 0.35f), new Vector3(x * 0.8f, yt, zf - 0.35f), 0.08f);
                Beam(mb, SubDark, new Vector3(-x * 0.8f, yt, zr + 0.15f), new Vector3(x * 0.8f, yt, zr + 0.15f), 0.08f);
                Beam(mb, SubDark, new Vector3(-x * 0.8f, yt, zf - 0.35f), new Vector3(-x * 0.8f, yt, zr + 0.15f), 0.08f);
                Beam(mb, SubDark, new Vector3(x * 0.8f, yt, zf - 0.35f), new Vector3(x * 0.8f, yt, zr + 0.15f), 0.08f);
                // Řidič s helmou, sedačka, rezerva
                mb.Box(SubDark, new Vector3(0f, 1.15f, -0.35f), new Vector3(0.6f, 0.4f, 0.5f));
                mb.Ball(SubTrim, new Vector3(0f, 1.48f, -0.25f), 0.2f, 4, 7);
                mb.Wheel(SubDark, SubTrim, new Vector3(0f, 1.25f, -1.45f), 0.34f, 0.25f, 8);
                // Blatníky nad koly
                foreach (float z in new[] { shape.front, shape.rear })
                    for (int side = -1; side <= 1; side += 2)
                        mb.Box(SubPaint, new Vector3(side * shape.openWheelX, shape.wheelRadius * 2f + 0.22f, z), new Vector3(0.46f, 0.06f, 1.0f));
                break;
            }
            case BodyStyle.Formula:
            {
                // Bočnice, kokpit, airbox, přední a zadní křídlo
                mb.Box(SubPaint, new Vector3(-0.55f, 0.48f, -0.4f), new Vector3(0.36f, 0.34f, 1.5f));
                mb.Box(SubPaint, new Vector3(0.55f, 0.48f, -0.4f), new Vector3(0.36f, 0.34f, 1.5f));
                mb.Box(SubDark, new Vector3(0f, 0.86f, 0.05f), new Vector3(0.5f, 0.06f, 0.8f));
                mb.Ball(SubTrim, new Vector3(0f, 0.98f, -0.05f), 0.2f, 4, 7);
                mb.Box(SubPaint, new Vector3(0f, 1.06f, -0.6f), new Vector3(0.32f, 0.36f, 0.7f));
                mb.Box(SubDark, new Vector3(0f, 0.32f, 2.25f), new Vector3(1.8f, 0.06f, 0.42f));
                mb.Box(SubPaint, new Vector3(-0.88f, 0.4f, 2.25f), new Vector3(0.04f, 0.2f, 0.45f));
                mb.Box(SubPaint, new Vector3(0.88f, 0.4f, 2.25f), new Vector3(0.04f, 0.2f, 0.45f));
                mb.Box(SubDark, new Vector3(0f, 1.15f, -1.9f), new Vector3(1.3f, 0.08f, 0.4f));
                mb.Box(SubPaint, new Vector3(0f, 1.32f, -1.95f), new Vector3(1.3f, 0.06f, 0.28f));
                mb.Box(SubPaint, new Vector3(-0.66f, 1.05f, -1.9f), new Vector3(0.04f, 0.6f, 0.5f));
                mb.Box(SubPaint, new Vector3(0.66f, 1.05f, -1.9f), new Vector3(0.04f, 0.6f, 0.5f));
                mb.Box(SubDark, new Vector3(0f, 0.85f, -1.75f), new Vector3(0.1f, 0.4f, 0.1f));
                break;
            }
        }
    }

    // ================================================================ vzory laku

    /// <summary>Plochý pás po povrchu karoserie mezi dvěma body.</summary>
    private static void Strip(MeshBuilder mb, int sub, Vector3 a, Vector3 b, float width, Vector3 normal)
    {
        Vector3 d = b - a;
        if (d.sqrMagnitude < 0.0001f) return;
        mb.Box(sub, (a + b) * 0.5f, new Vector3(width, 0.012f, d.magnitude + 0.01f), Quaternion.LookRotation(d, normal));
    }

    private static void AddPattern(MeshBuilder mb, Shape shape, int pattern)
    {
        Station[] body = shape.body;
        switch (pattern)
        {
            case 1: // Závodní pruhy přes kapotu, střechu a kufr
                foreach (float x in new[] { -0.22f, 0.22f })
                {
                    Station[] path = shape.cabin != null ? MergeTop(body, shape.cabin) : body;
                    for (int i = 0; i < path.Length - 1; i++)
                        Strip(mb, SubTrim, new Vector3(x, path[i].top + 0.008f, path[i].z), new Vector3(x, path[i + 1].top + 0.008f, path[i + 1].z), 0.16f, Vector3.up);
                }
                break;

            case 2: // Pásy po bocích
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < body.Length - 1; i++)
                    {
                        float y0 = Mathf.Lerp(body[i].bottom, body[i].top, 0.42f), y1 = Mathf.Lerp(body[i + 1].bottom, body[i + 1].top, 0.42f);
                        Strip(mb, SubTrim, new Vector3(side * (body[i].halfWidth + 0.008f), y0, body[i].z),
                            new Vector3(side * (body[i + 1].halfWidth + 0.008f), y1, body[i + 1].z), 0.14f, new Vector3(side, 0f, 0f));
                    }
                break;

            case 3: // Šachovnice na střeše (nebo kapotě)
            {
                Station a, b;
                if (shape.cabin != null) { a = shape.cabin[1]; b = shape.cabin[2]; }
                else { a = Interpolate(body, 0.3f); b = Interpolate(body, 1.1f); }
                float top = Mathf.Max(a.top, b.top) + 0.01f;
                float w = Mathf.Min(a.halfWidth, b.halfWidth) * 1.4f, len = Mathf.Abs(b.z - a.z) * 0.85f;
                const int cols = 4, rows = 4;
                for (int x = 0; x < cols; x++)
                    for (int z = 0; z < rows; z++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + (x + 0.5f) * w / cols, top, Mathf.Min(a.z, b.z) + Mathf.Abs(b.z - a.z) * 0.075f + (z + 0.5f) * len / rows);
                        mb.Box((x + z) % 2 == 0 ? SubTrim : SubDark, p, new Vector3(w / cols, 0.012f, len / rows));
                    }
                break;
            }
        }
    }

    /// <summary>Horní obrys auta: karoserie, kde je kabina tak její střecha.</summary>
    private static Station[] MergeTop(Station[] body, Station[] cabin)
    {
        var list = new List<Station>();
        foreach (var s in body)
            if (s.z < cabin[0].z || s.z > cabin[cabin.Length - 1].z) list.Add(s);
        foreach (var s in cabin) list.Add(s);
        list.Sort((p, q) => p.z.CompareTo(q.z));
        return list.ToArray();
    }

    // ================================================================ startovní čísla

    // Sedmisegmentový displej: a (nahoře), b (vpravo nahoře), c (vpravo dole), d (dole), e (vlevo dole), f (vlevo nahoře), g (uprostřed)
    private static readonly int[] DigitSegments = { 0x3F, 0x06, 0x5B, 0x4F, 0x66, 0x6D, 0x7D, 0x07, 0x7F, 0x6F };

    private static void AddNumbers(MeshBuilder mb, BodyStyle style, Shape shape, int number, bool onRoof = true)
    {
        number = Mathf.Clamp(number, 0, 99);
        if (shape.cabin != null && onRoof)
        {
            // Na střeše (čitelné shora) - kolečko + číslo
            Station roof = shape.cabin[1];
            float roofZ = (shape.cabin[1].z + shape.cabin[2].z) * 0.5f;
            Vector3 center = new Vector3(0f, roof.top + 0.012f, roofZ);
            mb.Cylinder(SubTrim, center - Vector3.up * 0.01f, 0.42f, 0.42f, 0.02f, 10);
            DrawNumber(mb, number, center + Vector3.up * 0.015f, Vector3.right, Vector3.forward, 0.42f);
        }

        // Na dveřích - čtené zvenku, horní strana čísla nahoru
        float doorZ = shape.cabin != null ? (shape.cabin[1].z + shape.cabin[2].z) * 0.5f : -0.2f;
        Station side = Interpolate(shape.body, doorZ);
        float y = Mathf.Lerp(side.bottom, side.top, 0.5f) + 0.03f;
        float size = Mathf.Min(0.38f, (side.top - side.bottom) * 0.6f);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 normal = new Vector3(s, 0f, 0f);
            Vector3 center = new Vector3(s * (side.halfWidth + 0.012f), y, doorZ);
            // Směr "doprava" pro diváka, který se dívá na bok auta zvenku
            Vector3 readRight = s > 0 ? Vector3.forward : Vector3.back;
            mb.Box(SubTrim, center - normal * 0.005f, new Vector3(0.01f, size * 1.35f, size * 1.6f));
            DrawNumber(mb, number, center + normal * 0.008f, readRight, Vector3.up, size);
        }
    }

    private static void DrawNumber(MeshBuilder mb, int number, Vector3 center, Vector3 right, Vector3 up, float height)
    {
        string text = number.ToString();
        float width = height * 0.55f, gap = height * 0.18f;
        float total = text.Length * width + (text.Length - 1) * gap;
        for (int i = 0; i < text.Length; i++)
        {
            float cx = -total * 0.5f + width * 0.5f + i * (width + gap);
            DrawDigit(mb, text[i] - '0', center + right * cx, right, up, width, height);
        }
    }

    private static void DrawDigit(MeshBuilder mb, int digit, Vector3 c, Vector3 right, Vector3 up, float w, float h)
    {
        int mask = DigitSegments[Mathf.Clamp(digit, 0, 9)];
        float t = h * 0.14f;
        Vector3 normal = Vector3.Cross(right, up);
        Quaternion rot = Quaternion.LookRotation(normal, up);
        // (střed x, střed y, šířka, výška) v jednotkách číslice
        Vector4[] seg =
        {
            new Vector4(0f, h * 0.5f - t * 0.5f, w, t), new Vector4(w * 0.5f - t * 0.5f, h * 0.25f, t, h * 0.5f),
            new Vector4(w * 0.5f - t * 0.5f, -h * 0.25f, t, h * 0.5f), new Vector4(0f, -h * 0.5f + t * 0.5f, w, t),
            new Vector4(-w * 0.5f + t * 0.5f, -h * 0.25f, t, h * 0.5f), new Vector4(-w * 0.5f + t * 0.5f, h * 0.25f, t, h * 0.5f),
            new Vector4(0f, 0f, w, t),
        };
        for (int i = 0; i < 7; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            Vector3 p = c + right * seg[i].x + up * seg[i].y;
            // Kvádr v rovině čísla: lokální x = šířka, y = výška, z = tloušťka
            Vector3 localSize = new Vector3(seg[i].z, seg[i].w, 0.01f);
            Vector3 lx = rot * Vector3.right;
            // Pokud lokální x míří proti "right", šířka je stejná (kvádr je symetrický)
            mb.Box(SubDark, p, Mathf.Abs(Vector3.Dot(lx, right)) > 0.5f ? localSize : new Vector3(localSize.y, localSize.x, localSize.z), rot);
        }
    }

    // ================================================================ kola

    private static readonly Dictionary<string, Mesh> wheelMeshes = new Dictionary<string, Mesh>();

    private static Mesh WheelMesh(Shape shape)
    {
        string key = shape.wheelRadius + "_" + shape.wheelWidth;
        if (wheelMeshes.TryGetValue(key, out Mesh cached) && cached != null) return cached;
        var mb = new MeshBuilder(2);
        float r = shape.wheelRadius, w = shape.wheelWidth;
        mb.Wheel(0, 0, Vector3.zero, r, w, 10);
        mb.Wheel(1, 1, Vector3.zero, r * 0.55f, w + 0.03f, 6);
        mb.Box(1, Vector3.zero, new Vector3(w + 0.04f, r * 1.35f, 0.08f));
        mb.Box(1, Vector3.zero, new Vector3(w + 0.04f, 0.08f, r * 1.35f));
        Mesh mesh = mb.ToMesh("Wheel");
        wheelMeshes[key] = mesh;
        return mesh;
    }

    private static Transform CreateWheel(Transform root, string name, Vector3 localPos, Shape shape, Palette palette, int layer,
        CarStats stats, out WheelCollider collider)
    {
        var colliderGo = new GameObject(name + "Collider");
        colliderGo.layer = layer;
        colliderGo.transform.SetParent(root, false);
        colliderGo.transform.localPosition = localPos;

        collider = colliderGo.AddComponent<WheelCollider>();
        collider.radius = shape.wheelRadius;
        collider.mass = 20f;
        collider.suspensionDistance = 0.2f;
        collider.forceAppPointDistance = 0.1f;
        collider.wheelDampingRate = 0.5f;
        collider.suspensionSpring = new JointSpring { spring = 22000f * stats.mass / 500f, damper = 2200f * stats.mass / 500f, targetPosition = 0.5f };
        // Arkádová přilnavost (násobená vlastností auta)
        collider.forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.4f, extremumValue = 1.6f, asymptoteSlip = 0.8f, asymptoteValue = 1.0f, stiffness = 1.5f * stats.grip
        };
        collider.sidewaysFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.25f, extremumValue = 1.5f, asymptoteSlip = 0.5f, asymptoteValue = 1.0f, stiffness = 1.8f * stats.grip
        };

        var visual = new GameObject(name);
        visual.layer = layer;
        visual.transform.SetParent(root, false);
        visual.transform.localPosition = localPos;
        visual.AddComponent<MeshFilter>().sharedMesh = WheelMesh(shape);
        visual.AddComponent<MeshRenderer>().sharedMaterials = new[] { palette.tire, palette.hub };
        return visual.transform;
    }
}
