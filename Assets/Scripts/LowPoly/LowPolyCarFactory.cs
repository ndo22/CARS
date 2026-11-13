using UnityEngine;

/// <summary>
/// Postaví kompletní low-poly auto: karoserie z "řezů" (prstenců) podél délky auta,
/// prosklená kabina, světla, kola, WheelCollidery, Rigidbody a skripty <see cref="Car"/>/<see cref="AICar"/>.
/// </summary>
public static class LowPolyCarFactory
{
    public enum BodyStyle { Sport, Muscle, Hatch }

    /// <summary>Jeden řez karoserií: poloha na délku, poloviční šířka, spodní a horní výška.</summary>
    private struct Station
    {
        public float z, halfWidth, bottom, top;
        public Station(float z, float halfWidth, float bottom, float top)
        {
            this.z = z; this.halfWidth = halfWidth; this.bottom = bottom; this.top = top;
        }
    }

    private const int SubPaint = 0, SubGlass = 1, SubDark = 2, SubHeadlight = 3, SubTaillight = 4, SubTrim = 5;
    private const float WheelRadius = 0.36f;
    private const float WheelWidth = 0.3f;

    public class Palette
    {
        public Material paint, glass, dark, headlight, taillight, trim, tire, hub;
    }

    public static GameObject Build<TController>(string name, Vector3 position, Quaternion rotation, BodyStyle style,
        Palette palette, int layer, out TController controller) where TController : CarController
    {
        // Vše skládáme v neaktivním objektu - Awake skriptů se zavolá až po nastavení všech odkazů
        var root = new GameObject(name);
        root.SetActive(false);
        root.layer = layer;
        root.transform.SetPositionAndRotation(position, rotation);

        Station[] body, cabin;
        float wheelBaseFront, wheelBaseRear, track;
        GetShape(style, out body, out cabin, out wheelBaseFront, out wheelBaseRear, out track);
        Station[] shell = WithWheelArches(body, wheelBaseFront, wheelBaseRear);

        // --- karoserie
        var mb = new MeshBuilder(6);
        Loft(mb, shell, 0.12f, SubPaint, null, SubDark);
        // Kabina: přední a zadní šikmá plocha jsou okna, prostředek je střecha
        Loft(mb, cabin, 0.1f, SubGlass, new[] { SubGlass, SubPaint, SubGlass }, SubGlass);
        AddDetails(mb, style, body);

        var bodyGo = new GameObject("Body");
        bodyGo.layer = layer;
        bodyGo.transform.SetParent(root.transform, false);
        bodyGo.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name + "Body");
        bodyGo.AddComponent<MeshRenderer>().sharedMaterials = new[]
        {
            palette.paint, palette.glass, palette.dark, palette.headlight, palette.taillight, palette.trim
        };

        // --- fyzika
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 500f;
        rb.drag = 0.05f;
        rb.angularDrag = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        float length = body[body.Length - 1].z - body[0].z;
        float midZ = (body[body.Length - 1].z + body[0].z) * 0.5f;
        var lower = root.AddComponent<BoxCollider>();
        lower.center = new Vector3(0f, 0.72f, midZ);
        lower.size = new Vector3(track + 0.25f, 0.6f, length);
        var upper = root.AddComponent<BoxCollider>();
        float cabMid = (cabin[0].z + cabin[cabin.Length - 1].z) * 0.5f;
        upper.center = new Vector3(0f, 1.15f, cabMid);
        upper.size = new Vector3(track - 0.2f, 0.45f, cabin[cabin.Length - 1].z - cabin[0].z);

        var com = new GameObject("CenterOfMass").transform;
        com.SetParent(root.transform, false);
        com.localPosition = new Vector3(0f, 0.4f, 0.05f);

        // --- kola (vnější bok pneumatiky lícuje s karoserií, kolo je vidět v podběhu)
        float wheelX = Mathf.Min(Interpolate(body, wheelBaseFront).halfWidth, Interpolate(body, wheelBaseRear).halfWidth) - WheelWidth * 0.5f + 0.03f;
        float wheelY = 0.5f;
        var lf = CreateWheel(root.transform, "LeftFront", new Vector3(-wheelX, wheelY, wheelBaseFront), palette, layer, out WheelCollider lfc);
        var rf = CreateWheel(root.transform, "RightFront", new Vector3(wheelX, wheelY, wheelBaseFront), palette, layer, out WheelCollider rfc);
        var lb = CreateWheel(root.transform, "LeftBack", new Vector3(-wheelX, wheelY, wheelBaseRear), palette, layer, out WheelCollider lbc);
        var rb2 = CreateWheel(root.transform, "RightBack", new Vector3(wheelX, wheelY, wheelBaseRear), palette, layer, out WheelCollider rbc);

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
        controller.motorTorque = 350f;
        controller.maxSteer = 28f;

        return root;
    }

    private static void GetShape(BodyStyle style, out Station[] body, out Station[] cabin,
        out float front, out float rear, out float track)
    {
        switch (style)
        {
            case BodyStyle.Muscle:
                body = new[]
                {
                    new Station(-2.25f, 0.88f, 0.4f, 0.95f), new Station(-2.05f, 0.98f, 0.32f, 1.0f),
                    new Station(-0.9f, 1.0f, 0.3f, 1.0f), new Station(1.3f, 1.0f, 0.3f, 0.98f),
                    new Station(2.1f, 0.98f, 0.32f, 0.88f), new Station(2.3f, 0.9f, 0.4f, 0.75f),
                };
                cabin = new[]
                {
                    new Station(-1.5f, 0.82f, 0.98f, 1.0f), new Station(-1.05f, 0.78f, 0.98f, 1.45f),
                    new Station(0.1f, 0.76f, 0.98f, 1.48f), new Station(0.75f, 0.84f, 0.96f, 1.0f),
                };
                front = 1.45f; rear = -1.4f; track = 1.7f;
                break;

            case BodyStyle.Hatch:
                body = new[]
                {
                    new Station(-1.75f, 0.85f, 0.4f, 1.0f), new Station(-1.6f, 0.92f, 0.32f, 1.05f),
                    new Station(1.0f, 0.92f, 0.32f, 0.95f), new Station(1.65f, 0.88f, 0.34f, 0.85f),
                    new Station(1.85f, 0.8f, 0.42f, 0.72f),
                };
                cabin = new[]
                {
                    new Station(-1.6f, 0.8f, 1.03f, 1.05f), new Station(-1.5f, 0.78f, 1.03f, 1.62f),
                    new Station(0.2f, 0.76f, 1.0f, 1.62f), new Station(1.0f, 0.84f, 0.94f, 0.96f),
                };
                front = 1.15f; rear = -1.15f; track = 1.6f;
                break;

            default: // Sport
                body = new[]
                {
                    new Station(-2.1f, 0.85f, 0.42f, 0.88f), new Station(-1.95f, 0.98f, 0.3f, 0.95f),
                    new Station(-0.5f, 1.0f, 0.28f, 0.92f), new Station(1.1f, 0.98f, 0.28f, 0.83f),
                    new Station(1.95f, 0.9f, 0.3f, 0.66f), new Station(2.15f, 0.78f, 0.36f, 0.56f),
                };
                cabin = new[]
                {
                    new Station(-1.45f, 0.78f, 0.9f, 0.95f), new Station(-0.85f, 0.72f, 0.88f, 1.3f),
                    new Station(0.1f, 0.7f, 0.86f, 1.33f), new Station(0.95f, 0.8f, 0.8f, 0.84f),
                };
                front = 1.35f; rear = -1.3f; track = 1.68f;
                break;
        }
    }

    /// <summary>
    /// "Loft" - spojí řezy do uzavřeného tělesa. Každý řez je šestiúhelník se zkosenými horními hranami.
    /// sideSub = boky, topSubPerSegment = horní plochy pro každý úsek (null = stejné jako boky), bottomSub = podvozek.
    /// </summary>
    private static void Loft(MeshBuilder mb, Station[] st, float chamfer, int sideSub, int[] topSubPerSegment, int bottomSub)
    {
        Vector3[][] rings = new Vector3[st.Length][];
        Vector3 center = Vector3.zero;
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
            center += new Vector3(0f, (s.top + s.bottom) * 0.5f, s.z);
        }
        center /= st.Length;

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

        // Čela (předek a zadek)
        CapRing(mb, rings[0], sideSub, Vector3.back);
        CapRing(mb, rings[st.Length - 1], sideSub, Vector3.forward);
    }

    /// <summary>Lineární interpolace profilu karoserie v místě z.</summary>
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
    private static Station[] WithWheelArches(Station[] st, float frontAxle, float rearAxle)
    {
        var zs = new System.Collections.Generic.List<float>();
        foreach (var s in st) zs.Add(s.z);
        foreach (float axle in new[] { frontAxle, rearAxle })
        {
            zs.Add(axle - WheelRadius - 0.14f);
            zs.Add(axle - WheelRadius * 0.7f);
            zs.Add(axle + WheelRadius * 0.7f);
            zs.Add(axle + WheelRadius + 0.14f);
        }
        zs.Sort();

        var result = new System.Collections.Generic.List<Station>();
        foreach (float z in zs)
        {
            if (result.Count > 0 && Mathf.Abs(result[result.Count - 1].z - z) < 0.02f) continue;
            Station s = Interpolate(st, z);
            foreach (float axle in new[] { frontAxle, rearAxle })
            {
                if (Mathf.Abs(z - axle) <= WheelRadius * 0.7f + 0.001f)
                    s.bottom = Mathf.Min(s.top - 0.1f, 0.74f);
            }
            result.Add(s);
        }
        return result.ToArray();
    }

    private static void CapRing(MeshBuilder mb, Vector3[] ring, int sub, Vector3 facing)
    {
        Vector3 c = Vector3.zero;
        foreach (var v in ring) c += v;
        c /= ring.Length;
        for (int k = 0; k < ring.Length; k++)
            mb.TriFacing(sub, c, ring[k], ring[(k + 1) % ring.Length], facing);
    }

    private static void AddDetails(MeshBuilder mb, BodyStyle style, Station[] body)
    {
        Station front = body[body.Length - 1];
        Station rear = body[0];

        // Světla
        float lightY = Mathf.Lerp(front.bottom, front.top, 0.6f);
        for (int side = -1; side <= 1; side += 2)
        {
            mb.Box(SubHeadlight, new Vector3(side * front.halfWidth * 0.65f, lightY, front.z + 0.01f), new Vector3(0.35f, 0.12f, 0.06f));
            mb.Box(SubTaillight, new Vector3(side * rear.halfWidth * 0.65f, Mathf.Lerp(rear.bottom, rear.top, 0.7f), rear.z - 0.01f), new Vector3(0.38f, 0.12f, 0.06f));
        }

        // Nárazníky
        mb.Box(SubDark, new Vector3(0f, front.bottom + 0.06f, front.z + 0.02f), new Vector3(front.halfWidth * 2f, 0.14f, 0.1f));
        mb.Box(SubDark, new Vector3(0f, rear.bottom + 0.06f, rear.z - 0.02f), new Vector3(rear.halfWidth * 2f, 0.14f, 0.1f));

        // Spoiler / doplňky podle stylu
        if (style == BodyStyle.Sport)
        {
            float y = rear.top + 0.28f;
            mb.Box(SubPaint, new Vector3(0f, y, rear.z + 0.2f), new Vector3(1.7f, 0.06f, 0.35f));
            mb.Box(SubDark, new Vector3(-0.55f, y - 0.15f, rear.z + 0.22f), new Vector3(0.06f, 0.26f, 0.15f));
            mb.Box(SubDark, new Vector3(0.55f, y - 0.15f, rear.z + 0.22f), new Vector3(0.06f, 0.26f, 0.15f));
        }
        else if (style == BodyStyle.Muscle)
        {
            // Nasávání na kapotě a závodní pruhy
            mb.Box(SubDark, new Vector3(0f, 1.02f, 1.3f), new Vector3(0.5f, 0.1f, 0.8f));
            mb.Box(SubTrim, new Vector3(-0.25f, 1.005f, -0.35f), new Vector3(0.18f, 0.02f, 3.3f));
            mb.Box(SubTrim, new Vector3(0.25f, 1.005f, -0.35f), new Vector3(0.18f, 0.02f, 3.3f));
        }
        else
        {
            // Střešní nosič
            mb.Box(SubDark, new Vector3(-0.55f, 1.68f, -0.6f), new Vector3(0.06f, 0.06f, 1.6f));
            mb.Box(SubDark, new Vector3(0.55f, 1.68f, -0.6f), new Vector3(0.06f, 0.06f, 1.6f));
        }
    }

    private static Transform CreateWheel(Transform root, string name, Vector3 localPos, Palette palette, int layer, out WheelCollider collider)
    {
        var colliderGo = new GameObject(name + "Collider");
        colliderGo.layer = layer;
        colliderGo.transform.SetParent(root, false);
        colliderGo.transform.localPosition = localPos;

        collider = colliderGo.AddComponent<WheelCollider>();
        collider.radius = WheelRadius;
        collider.mass = 20f;
        collider.suspensionDistance = 0.2f;
        collider.forceAppPointDistance = 0.1f;
        collider.wheelDampingRate = 0.5f;
        collider.suspensionSpring = new JointSpring { spring = 22000f, damper = 2200f, targetPosition = 0.5f };
        // Arkádová přilnavost
        collider.forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.4f, extremumValue = 1.6f, asymptoteSlip = 0.8f, asymptoteValue = 1.0f, stiffness = 1.5f
        };
        collider.sidewaysFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.25f, extremumValue = 1.5f, asymptoteSlip = 0.5f, asymptoteValue = 1.0f, stiffness = 1.8f
        };

        var mb = new MeshBuilder(2);
        mb.Wheel(0, 0, Vector3.zero, WheelRadius, WheelWidth, 10);
        // Disk kola (šestiúhelník) a paprsky
        mb.Wheel(1, 1, Vector3.zero, WheelRadius * 0.55f, WheelWidth + 0.03f, 6);
        mb.Box(1, Vector3.zero, new Vector3(WheelWidth + 0.04f, WheelRadius * 1.35f, 0.08f));
        mb.Box(1, Vector3.zero, new Vector3(WheelWidth + 0.04f, 0.08f, WheelRadius * 1.35f));

        var visual = new GameObject(name);
        visual.layer = layer;
        visual.transform.SetParent(root, false);
        visual.transform.localPosition = localPos;
        visual.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
        visual.AddComponent<MeshRenderer>().sharedMaterials = new[] { palette.tire, palette.hub };
        return visual.transform;
    }
}
