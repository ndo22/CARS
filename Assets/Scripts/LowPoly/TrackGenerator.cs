using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurálně postaví low-poly okruh: silnici, obrubníky, krajnice, svodidla, startovní bránu,
/// tribunu, pneumatiky v zatáčkách, checkpointy, terén, vodu a dekorace podle tématu.
/// Trať vede hladkou křivkou (Catmull-Rom) přes body <see cref="TrackSettings.controlPoints"/>.
/// </summary>
public static class TrackGenerator
{
    [Serializable]
    public class TrackSettings
    {
        [Tooltip("Body, kterými trať prochází (x, výška, z). Trať je uzavřená.")]
        public Vector3[] controlPoints = DefaultControlPoints();
        public float roadWidth = 14f;
        [Tooltip("Vzdálenost svodidel od okraje silnice.")]
        public float barrierOffset = 5f;
        [Tooltip("Délka jednoho segmentu silnice v metrech - větší = hranatější low-poly vzhled.")]
        public float segmentLength = 4f;
        public float checkpointSpacing = 24f;
        public float heightScale = 2f;
        public int seed = 1234;
        public int treeCount = 600;
        public int rockCount = 70;
        public int cloudCount = 18;
        [Tooltip("Postavit lampy i ve dne (pro závod, kde se během jízdy setmí).")]
        public bool forceLamps;
    }

    public class TrackResult
    {
        public Vector3[] centers;
        public Vector3[] forwards;
        public Vector3[] rights;
        public float length;
        public Vector3 Center(int i) => centers[Mod(i, centers.Length)];
        public Vector3 Forward(int i) => forwards[Mod(i, forwards.Length)];
        public Vector3 Right(int i) => rights[Mod(i, rights.Length)];

        // Prostorový index (mřížka) pro rychlé hledání nejbližšího bodu trati
        internal const float IndexCell = 16f;
        internal Dictionary<long, List<int>> index;
        internal Vector2 indexMin;

        internal void BuildIndex()
        {
            index = new Dictionary<long, List<int>>();
            indexMin = new Vector2(float.MaxValue, float.MaxValue);
            foreach (var c in centers) indexMin = Vector2.Min(indexMin, new Vector2(c.x, c.z));
            for (int i = 0; i < centers.Length; i++)
            {
                long key = Key(Cell(centers[i].x, indexMin.x), Cell(centers[i].z, indexMin.y));
                if (!index.TryGetValue(key, out var list)) index[key] = list = new List<int>();
                list.Add(i);
            }
        }

        internal static int Cell(float v, float min) => Mathf.FloorToInt((v - min) / IndexCell);
        internal static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
    }

    /// <summary>Výsledek kontroly tvaru tratě.</summary>
    public struct Validation
    {
        public float length, minRadius, minGap, maxSlope;
        public bool ok;
        public string message;
    }

    /// <summary>
    /// Zkontroluje, jestli je trať jezditelná: nejostřejší zatáčka, nejmenší vzdálenost dvou různých částí tratě
    /// (aby se nekřížily ani nedotýkaly svodidly) a největší stoupání.
    /// </summary>
    public static Validation Validate(TrackSettings s)
    {
        TrackResult t = Sample(s);
        int n = t.centers.Length;
        var v = new Validation { length = t.length, minRadius = float.MaxValue, minGap = float.MaxValue };
        for (int i = 0; i < n; i++)
        {
            Vector2 a = Flat(t.Center(i - 1)), b = Flat(t.Center(i)), c = Flat(t.Center(i + 1));
            float area = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            if (area > 1e-5f)
                v.minRadius = Mathf.Min(v.minRadius, Vector2.Distance(a, b) * Vector2.Distance(b, c) * Vector2.Distance(c, a) / (4f * area));
            float run = Mathf.Max(0.01f, Vector2.Distance(a, b));
            v.maxSlope = Mathf.Max(v.maxSlope, Mathf.Abs(t.Center(i).y - t.Center(i - 1).y) / run);
        }
        int skip = Mathf.CeilToInt(80f / s.segmentLength);
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                if (Mathf.Min(j - i, n - (j - i)) < skip) continue;
                v.minGap = Mathf.Min(v.minGap, Vector2.Distance(Flat(t.centers[i]), Flat(t.centers[j])));
            }

        float needGap = s.roadWidth + s.barrierOffset * 2f + 8f;
        float needRadius = s.roadWidth * 0.5f + s.barrierOffset + 2f;
        if (n < 20 || t.length < 300f) v.message = "TRACK IS TOO SHORT";
        else if (v.minGap < needGap) v.message = "TRACK OVERLAPS ITSELF";
        else if (v.minRadius < needRadius) v.message = "A CORNER IS TOO TIGHT";
        else if (v.maxSlope > 0.12f) v.message = "A HILL IS TOO STEEP";
        else { v.ok = true; v.message = "TRACK OK"; }
        return v;
    }

    private static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

    /// <summary>Materiály podle klíčů barev (viz <see cref="TrackTheme"/>).</summary>
    public interface IPalette
    {
        Material Get(string key);
    }

    public static Vector3[] DefaultControlPoints()
    {
        return new[]
        {
            new Vector3(0, 0, -80), new Vector3(0, 0, 60), new Vector3(8, 0.5f, 120), new Vector3(45, 1.5f, 155),
            new Vector3(100, 3, 160), new Vector3(140, 4, 130), new Vector3(160, 4, 80), new Vector3(200, 3, 55),
            new Vector3(250, 2, 70), new Vector3(285, 2, 115), new Vector3(330, 3, 140), new Vector3(380, 4, 120),
            new Vector3(395, 4, 60), new Vector3(370, 3, 0), new Vector3(320, 2, -25), new Vector3(300, 1, -75),
            new Vector3(320, 1, -130), new Vector3(300, 1, -185), new Vector3(240, 1, -200), new Vector3(170, 2, -175),
            new Vector3(120, 2, -200), new Vector3(60, 1, -195), new Vector3(15, 0, -150),
        };
    }

    private static int Mod(int i, int n) => ((i % n) + n) % n;
    private static float Rand(System.Random rng) => (float)rng.NextDouble();
    private static float Rand(System.Random rng, float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

    public static TrackResult Build(Transform parent, TrackSettings s, TrackTheme theme, IPalette palette)
    {
        var rng = new System.Random(s.seed);
        TrackResult track = Sample(s);
        int n = track.centers.Length;

        // Zatáčky - kde se směr mění víc, dáme obrubníky a pneumatiky
        bool[] isCorner = new bool[n];
        for (int i = 0; i < n; i++)
            isCorner[i] = Vector3.Angle(track.Forward(i - 2), track.Forward(i + 2)) > 6f;
        bool[] curb = new bool[n];
        for (int i = 0; i < n; i++)
            for (int k = -2; k <= 2; k++)
                if (isCorner[Mod(i + k, n)]) curb[i] = true;

        BuildRoad(parent, track, curb, s, palette, rng);
        BuildBarriers(parent, track, s, palette);
        BuildStartGate(parent, track, s, palette);
        BuildTrackside(parent, track, isCorner, s, palette, rng);
        if (theme.night || s.forceLamps) BuildLamps(parent, track, s, palette);
        BuildCheckpoints(parent, track, s);
        BuildTerrainAndScenery(parent, track, s, theme, palette, rng);

        return track;
    }

    /// <summary>Body středové čáry tratě (pro náhled v editoru).</summary>
    public static Vector3[] SamplePath(TrackSettings s) => Sample(s).centers;

    /// <summary>Délka tratě v metrech (bez stavění geometrie) - pro výběr tratě v menu.</summary>
    public static float Length(TrackSettings s) => Sample(s).length;

    // ---------------------------------------------------------------- křivka

    private static TrackResult Sample(TrackSettings s)
    {
        Vector3[] p = s.controlPoints;
        int count = p.Length;
        var points = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            Vector3 p0 = Scale(p[Mod(i - 1, count)], s), p1 = Scale(p[i], s), p2 = Scale(p[Mod(i + 1, count)], s), p3 = Scale(p[Mod(i + 2, count)], s);
            int steps = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(p1, p2) / s.segmentLength));
            for (int j = 0; j < steps; j++)
                points.Add(CatmullRom(p0, p1, p2, p3, j / (float)steps));
        }

        // Start = bod nejblíž počátku souřadnic -> index 0
        int start = 0;
        float best = float.MaxValue;
        for (int i = 0; i < points.Count; i++)
        {
            float d = new Vector2(points[i].x, points[i].z).sqrMagnitude;
            if (d < best) { best = d; start = i; }
        }

        int n = points.Count;
        var result = new TrackResult { centers = new Vector3[n], forwards = new Vector3[n], rights = new Vector3[n] };
        for (int i = 0; i < n; i++)
            result.centers[i] = points[Mod(start + i, n)];
        for (int i = 0; i < n; i++)
        {
            Vector3 f = result.Center(i + 1) - result.Center(i - 1);
            f.y = 0f;
            f.Normalize();
            result.forwards[i] = f;
            result.rights[i] = Vector3.Cross(Vector3.up, f).normalized;
            result.length += Vector3.Distance(result.Center(i), result.Center(i + 1));
        }
        result.BuildIndex();
        return result;
    }

    private static Vector3 Scale(Vector3 v, TrackSettings s) => new Vector3(v.x, v.y * s.heightScale, v.z);

    // Centripetální Catmull-Rom - nedělá smyčky ani přestřely
    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t0 = 0f;
        float t1 = t0 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector3.Distance(p0, p1)));
        float t2 = t1 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector3.Distance(p1, p2)));
        float t3 = t2 + Mathf.Sqrt(Mathf.Max(1e-4f, Vector3.Distance(p2, p3)));
        float u = Mathf.Lerp(t1, t2, t);

        Vector3 a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
        Vector3 a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
        Vector3 a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
        Vector3 b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
        Vector3 b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
        return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
    }

    // ---------------------------------------------------------------- silnice

    private static void BuildRoad(Transform parent, TrackResult t, bool[] curb, TrackSettings s, IPalette palette, System.Random rng)
    {
        const int asphaltA = 0, asphaltB = 1, line = 2, curbA = 3, curbB = 4;
        var mb = new MeshBuilder(5);
        var vergeMb = new MeshBuilder(2);
        int n = t.centers.Length;
        float hw = s.roadWidth * 0.5f;
        const float curbWidth = 1.4f;
        float vergeEnd = hw + s.barrierOffset + 4f;

        for (int i = 0; i < n; i++)
        {
            Vector3 c0 = t.Center(i), c1 = t.Center(i + 1);
            Vector3 r0 = t.Right(i), r1 = t.Right(i + 1);

            // Asfalt - dva odstíny střídané náhodně pro "plochý" low-poly vzhled
            Strip(mb, Rand(rng) < 0.5f ? asphaltA : asphaltB, c0, c1, r0, r1, -hw, hw, 0f);

            // Přerušovaná středová čára
            if (i % 3 == 0)
                Strip(mb, line, c0, c1, r0, r1, -0.15f, 0.15f, 0.02f);

            float edge = hw;
            if (curb[i])
            {
                int color = (i % 2 == 0) ? curbA : curbB;
                // Obrubník je mírně zvednutý a ke kraji se svažuje
                Strip(mb, color, c0, c1, r0, r1, hw, hw + curbWidth * 0.5f, 0.06f);
                Strip(mb, color, c0, c1, r0, r1, hw + curbWidth * 0.5f, hw + curbWidth, 0.03f);
                Strip(mb, color, c0, c1, r0, r1, -hw - curbWidth * 0.5f, -hw, 0.06f);
                Strip(mb, color, c0, c1, r0, r1, -hw - curbWidth, -hw - curbWidth * 0.5f, 0.03f);
                edge = hw + curbWidth;
            }
            else
            {
                Strip(mb, line, c0, c1, r0, r1, hw - 0.5f, hw - 0.2f, 0.02f);
                Strip(mb, line, c0, c1, r0, r1, -hw + 0.2f, -hw + 0.5f, 0.02f);
            }

            int verge = Rand(rng) < 0.5f ? 0 : 1;
            Strip(vergeMb, verge, c0, c1, r0, r1, edge, vergeEnd, -0.03f);
            Strip(vergeMb, verge, c0, c1, r0, r1, -vergeEnd, -edge, -0.03f);
        }

        var go = CreateMeshObject("Road", parent, mb.ToMesh("Road"), palette,
            "asphaltA", "asphaltB", "line", "curbA", "curbB");
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;

        // Krajnice zvlášť - podle jména collideru auto pozná, že jede mimo silnici (prach, zpomalení)
        var vergeGo = CreateMeshObject("Verge", parent, vergeMb.ToMesh("Verge"), palette, "vergeA", "vergeB");
        vergeGo.AddComponent<MeshCollider>().sharedMesh = vergeGo.GetComponent<MeshFilter>().sharedMesh;
        vergeGo.AddComponent<SurfaceType>().kind = SurfaceType.Kind.Grass;
    }

    /// <summary>Pruh rovnoběžný se středem trati mezi bočními odsazeními <paramref name="from"/> a <paramref name="to"/>.</summary>
    private static void Strip(MeshBuilder mb, int sub, Vector3 c0, Vector3 c1, Vector3 r0, Vector3 r1, float from, float to, float lift)
    {
        Vector3 up = Vector3.up * lift;
        mb.QuadFacing(sub, c0 + r0 * from + up, c1 + r1 * from + up, c1 + r1 * to + up, c0 + r0 * to + up, Vector3.up);
    }

    // ---------------------------------------------------------------- svodidla

    private static void BuildBarriers(Transform parent, TrackResult t, TrackSettings s, IPalette palette)
    {
        var mb = new MeshBuilder(2);
        int n = t.centers.Length;
        float offset = s.roadWidth * 0.5f + s.barrierOffset;
        const float height = 1.1f, thickness = 0.7f;

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < n; i++)
            {
                int sub = (i / 2) % 2;
                Vector3 c0 = t.Center(i) + Vector3.down * 0.3f, c1 = t.Center(i + 1) + Vector3.down * 0.3f;
                Vector3 r0 = t.Right(i) * side, r1 = t.Right(i + 1) * side;
                Vector3 up = Vector3.up * (height + 0.3f);

                Vector3 in0 = c0 + r0 * offset, in1 = c1 + r1 * offset;
                Vector3 out0 = c0 + r0 * (offset + thickness), out1 = c1 + r1 * (offset + thickness);

                Vector3 towardsRoad = -(r0 + r1);
                mb.QuadFacing(sub, in0, in1, in1 + up, in0 + up, towardsRoad);
                mb.QuadFacing(sub, in0 + up, in1 + up, out1 + up, out0 + up, Vector3.up);
                mb.QuadFacing(sub, out0, out1, out1 + up, out0 + up, -towardsRoad);
            }
        }

        var go = CreateMeshObject("Barriers", parent, mb.ToMesh("Barriers"), palette, "barrierA", "barrierB");
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    // ---------------------------------------------------------------- start

    private static void BuildStartGate(Transform parent, TrackResult t, TrackSettings s, IPalette palette)
    {
        var mb = new MeshBuilder(5);
        Vector3 c = t.Center(0);
        Vector3 f = t.Forward(0), r = t.Right(0);
        float hw = s.roadWidth * 0.5f;

        // Šachovnice přes silnici
        const int cols = 14, rows = 2;
        float cell = s.roadWidth / cols;
        for (int x = 0; x < cols; x++)
            for (int z = 0; z < rows; z++)
            {
                Vector3 o = c + r * (-hw + x * cell) + f * (z * cell - cell) + Vector3.up * 0.03f;
                mb.QuadFacing((x + z) % 2, o, o + f * cell, o + f * cell + r * cell, o + r * cell, Vector3.up);
            }

        // Startovní pozice (bílé "L" značky) pro rošt
        for (int k = 0; k < 6; k++)
        {
            int row = k / 2;
            float side = (k % 2 == 0) ? -3f : 3f;
            Vector3 p = t.Center(-3 - row * 2) + t.Right(-3 - row * 2) * side + t.Forward(-3) * ((k % 2) * -3f + 2.6f) + Vector3.up * 0.025f;
            Vector3 fr = t.Forward(-3), rr = t.Right(-3);
            mb.QuadFacing(0, p - rr * 1.1f, p - rr * 1.1f + fr * 0.25f, p + rr * 1.1f + fr * 0.25f, p + rr * 1.1f, Vector3.up);
        }

        // Brána: dva sloupy, nosník se šachovnicí a startovní světla
        Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
        float pillarX = hw + 2.5f;
        const float gateHeight = 6.5f;
        mb.Box(2, c + r * pillarX + Vector3.up * (gateHeight * 0.5f - 0.3f), new Vector3(0.8f, gateHeight, 0.8f), rot);
        mb.Box(2, c - r * pillarX + Vector3.up * (gateHeight * 0.5f - 0.3f), new Vector3(0.8f, gateHeight, 0.8f), rot);
        Vector3 beam = c + Vector3.up * gateHeight;
        int beamCells = cols + 4;
        float w = (pillarX * 2f + 0.8f) / beamCells;
        for (int x = 0; x < beamCells; x++)
        {
            Vector3 p = beam + r * (-pillarX - 0.4f + w * (x + 0.5f));
            mb.Box(x % 2, p + Vector3.up * 0.3f, new Vector3(w, 0.6f, 0.6f), rot);
            mb.Box((x + 1) % 2, p - Vector3.up * 0.3f, new Vector3(w, 0.6f, 0.6f), rot);
        }
        // Startovní semafor pod nosníkem
        mb.Box(4, beam - Vector3.up * 1.1f - f * 0.1f, new Vector3(3.2f, 0.9f, 0.4f), rot);
        for (int i = 0; i < 4; i++)
            mb.Box(3, beam - Vector3.up * 1.1f - f * 0.32f + r * (-1.2f + i * 0.8f), new Vector3(0.5f, 0.5f, 0.1f), rot);

        var go = CreateMeshObject("StartGate", parent, mb.ToMesh("StartGate"), palette,
            "checkerWhite", "checkerBlack", "metal", "startLight", "checkerBlack");
        // Ignore Raycast - aby se při respawnu auto neobjevilo na nosníku brány
        go.layer = 2;

        foreach (int side in new[] { -1, 1 })
        {
            var pillar = new GameObject("PillarCollider");
            pillar.layer = 2;
            pillar.transform.SetParent(go.transform, false);
            pillar.transform.SetPositionAndRotation(c + r * (pillarX * side) + Vector3.up * (gateHeight * 0.5f - 0.3f), rot);
            pillar.AddComponent<BoxCollider>().size = new Vector3(0.8f, gateHeight, 0.8f);
        }
    }

    // ---------------------------------------------------------------- okolí trati

    private static void BuildTrackside(Transform parent, TrackResult t, bool[] isCorner, TrackSettings s, IPalette palette, System.Random rng)
    {
        const int seat = 0, roof = 1, metal = 2, tire = 3, tireTop = 4, flagA = 5, flagB = 6;
        const int crowd0 = 7; // 7..10 barvy diváků
        var mb = new MeshBuilder(11);
        int n = t.centers.Length;
        float hw = s.roadWidth * 0.5f;
        float barrier = hw + s.barrierOffset;

        // --- Tribuna u startovní rovinky (vlevo od trati)
        int mid = 5;
        Vector3 c = t.Center(mid) + Vector3.down * 0.4f;
        Vector3 f = t.Forward(mid), outward = -t.Right(mid);
        Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
        const float standLength = 44f, tierDepth = 1.3f, tierHeight = 0.9f;
        const int tiers = 6;
        float start = barrier + 4f;
        for (int k = 0; k < tiers; k++)
        {
            float d = start + k * tierDepth;
            float h = (k + 1) * tierHeight;
            mb.Box(seat, c + outward * (d + tierDepth * 0.5f) + Vector3.up * (h * 0.5f), new Vector3(tierDepth, h, standLength), rot);
            // Diváci
            for (float z = -standLength * 0.5f + 0.8f; z < standLength * 0.5f - 0.5f; z += 1.1f)
            {
                if (Rand(rng) < 0.25f) continue;
                Vector3 p = c + outward * (d + tierDepth * 0.45f) + f * (z + Rand(rng, -0.2f, 0.2f)) + Vector3.up * (h + 0.45f);
                mb.Box(crowd0 + rng.Next(4), p, new Vector3(0.45f, 0.9f, 0.55f), rot);
            }
        }
        float back = start + tiers * tierDepth;
        float roofY = tiers * tierHeight + 4.2f;
        mb.Box(roof, c + outward * (back + 0.2f) + Vector3.up * (roofY * 0.5f), new Vector3(0.4f, roofY, standLength), rot);
        mb.Box(roof, c + outward * (start + tiers * tierDepth * 0.5f) + Vector3.up * roofY, new Vector3(tiers * tierDepth + 2.5f, 0.3f, standLength + 2f), rot);
        for (float z = -standLength * 0.5f; z <= standLength * 0.5f; z += standLength / 4f)
            mb.Box(metal, c + outward * (start - 0.5f) + f * z + Vector3.up * (roofY * 0.5f), new Vector3(0.25f, roofY, 0.25f), rot);

        // --- Vlajky podél startovní rovinky (vpravo)
        for (int k = -6; k <= 10; k += 2)
        {
            Vector3 basePos = t.Center(k) + t.Right(k) * (barrier + 1.6f) + Vector3.down * 0.4f;
            mb.Cylinder(metal, basePos, 0.08f, 0.06f, 6.5f, 5);
            Vector3 top = basePos + Vector3.up * 6.3f;
            Vector3 dir = t.Forward(k);
            int flag = (k / 2) % 2 == 0 ? flagA : flagB;
            mb.TriDoubleSided(flag, top, top - Vector3.up * 1.6f, top + dir * 2.2f - Vector3.up * 0.4f);
            mb.TriDoubleSided(flag, top, top + dir * 2.2f - Vector3.up * 0.4f, top + dir * 2.4f + Vector3.up * 0.1f);
        }

        // --- Stohy pneumatik na vnější straně zatáček
        for (int i = 0; i < n; i += 3)
        {
            if (!isCorner[i]) continue;
            float turn = Vector3.Cross(t.Forward(i - 2), t.Forward(i + 2)).y;
            float side = turn > 0f ? -1f : 1f; // vnější strana zatáčky
            Vector3 basePos = t.Center(i) + t.Right(i) * side * (barrier + 1.4f) + Vector3.down * 0.4f;
            for (int stack = -1; stack <= 1; stack += 2)
            {
                Vector3 p = basePos + t.Forward(i) * (stack * 0.6f);
                for (int level = 0; level < 3; level++)
                    mb.Cylinder(level == 2 ? tireTop : tire, p + Vector3.up * (level * 0.32f), 0.55f, 0.55f, 0.32f, 8, stack * 0.2f);
            }
        }

        CreateMeshObject("Trackside", parent, mb.ToMesh("Trackside"), palette,
            "seat", "roof", "metal", "tire", "curbB", "flagA", "flagB", "crowdA", "crowdB", "crowdC", "crowdD");
    }

    // ---------------------------------------------------------------- checkpointy

    private static void BuildCheckpoints(Transform parent, TrackResult t, TrackSettings s)
    {
        var root = new GameObject("Checkpoints");
        root.transform.SetParent(parent, false);

        int n = t.centers.Length;
        int every = Mathf.Max(1, Mathf.RoundToInt(s.checkpointSpacing / s.segmentLength));
        int count = n / every;
        int layer = LayerMask.NameToLayer("Checkpoint");
        var list = new List<Transform>();

        for (int k = 0; k < count; k++)
        {
            int i = k * every;
            var cp = new GameObject((k + 1).ToString());
            cp.AddComponent<Checkpoint>().number = k + 1;
            list.Add(cp.transform);
            if (layer >= 0) cp.layer = layer;
            cp.transform.SetParent(root.transform, false);
            cp.transform.SetPositionAndRotation(t.Center(i) + Vector3.up * 3f, Quaternion.LookRotation(t.Forward(i), Vector3.up));
            var box = cp.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(s.roadWidth + s.barrierOffset * 2f, 8f, 2f);
        }
        root.AddComponent<CheckpointSet>().Register(list.ToArray());
    }

    // ---------------------------------------------------------------- terén a krajina

    private class TerrainContext
    {
        public TrackResult track;
        public TrackTheme theme;
        public Vector3 min, max;
        public float flatZone, noiseOffset, waterLevel, trackMaxX, baseY;

        public float Height(float x, float z)
        {
            NearestOnTrack(track, x, z, out float dist, out float roadY);
            if (theme.flatGround) return roadY - 0.4f;
            float hills = (Mathf.PerlinNoise(x * 0.007f + noiseOffset, z * 0.007f + noiseOffset) - 0.4f) * theme.hillAmplitude
                        + (Mathf.PerlinNoise(x * 0.02f + noiseOffset, z * 0.02f) - 0.5f) * 8f;

            float edgeX = theme.seaEast ? x - min.x : Mathf.Min(x - min.x, max.x - x);
            float edge = Mathf.Min(edgeX, Mathf.Min(z - min.z, max.z - z));
            hills += Mathf.Max(0f, 120f - edge) / 120f * theme.mountainHeight; // hory na okraji mapy

            if (theme.seaEast)
                hills -= Mathf.Max(0f, x - (trackMaxX + 25f)) * 0.35f + 4f; // pobřeží klesá do moře

            float blend = Mathf.SmoothStep(0f, 1f, (dist - flatZone) / 80f);
            float floor = theme.hasWater ? waterLevel - 12f : roadY - 6f;
            return Mathf.Lerp(roadY - 0.4f, Mathf.Max(floor, hills + baseY), blend);
        }

        public float Distance(float x, float z)
        {
            NearestOnTrack(track, x, z, out float dist, out _);
            return dist;
        }
    }

    private static void BuildTerrainAndScenery(Transform parent, TrackResult t, TrackSettings s, TrackTheme theme, IPalette palette, System.Random rng)
    {
        var ctx = new TerrainContext { track = t, theme = theme };
        ctx.min = t.centers[0];
        ctx.max = t.centers[0];
        float minRoad = float.MaxValue;
        foreach (var c in t.centers)
        {
            ctx.min = Vector3.Min(ctx.min, c);
            ctx.max = Vector3.Max(ctx.max, c);
            minRoad = Mathf.Min(minRoad, c.y);
        }
        ctx.trackMaxX = ctx.max.x;
        const float margin = 260f, cell = 12f;
        ctx.min -= new Vector3(margin, 0, margin);
        ctx.max += new Vector3(margin, 0, margin);
        ctx.flatZone = s.roadWidth * 0.5f + s.barrierOffset + 8f;
        ctx.noiseOffset = s.seed % 1000;
        ctx.baseY = t.centers[0].y;
        ctx.waterLevel = minRoad - 1.6f;

        // --- Terén
        int nx = Mathf.CeilToInt((ctx.max.x - ctx.min.x) / cell) + 1;
        int nz = Mathf.CeilToInt((ctx.max.z - ctx.min.z) / cell) + 1;
        var grid = new Vector3[nx, nz];
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = ctx.min.x + ix * cell, z = ctx.min.z + iz * cell;
                float h = ctx.Height(x, z);
                if (!theme.flatGround && ctx.Distance(x, z) > ctx.flatZone + 15f)
                {
                    // Trochu náhodného posunu - typický low-poly "zubatý" terén
                    x += (Rand(rng) - 0.5f) * cell * 0.4f;
                    z += (Rand(rng) - 0.5f) * cell * 0.4f;
                    h += (Rand(rng) - 0.5f) * 1.2f;
                }
                grid[ix, iz] = new Vector3(x, h, z);
            }

        var terrain = new MeshBuilder(6);
        for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                Vector3 a = grid[ix, iz], b = grid[ix, iz + 1], c = grid[ix + 1, iz + 1], d = grid[ix + 1, iz];
                TerrainTri(terrain, a, b, c, ctx, rng);
                TerrainTri(terrain, a, c, d, ctx, rng);
            }
        var terrainGo = CreateMeshObject("Terrain", parent, terrain.ToMesh("Terrain"), palette,
            "groundA", "groundB", "groundC", "high", "peak", "low");
        terrainGo.AddComponent<MeshCollider>().sharedMesh = terrainGo.GetComponent<MeshFilter>().sharedMesh;
        terrainGo.AddComponent<SurfaceType>().kind = theme.flatGround ? SurfaceType.Kind.Road : SurfaceType.Kind.Grass;

        // --- Voda
        if (theme.hasWater)
        {
            var water = new MeshBuilder(2);
            const float wcell = 20f;
            int wx = Mathf.CeilToInt((ctx.max.x - ctx.min.x + 800f) / wcell), wz = Mathf.CeilToInt((ctx.max.z - ctx.min.z + 800f) / wcell);
            var wgrid = new Vector3[wx + 1, wz + 1];
            for (int ix = 0; ix <= wx; ix++)
                for (int iz = 0; iz <= wz; iz++)
                    wgrid[ix, iz] = new Vector3(ctx.min.x - 400f + ix * wcell, ctx.waterLevel + Rand(rng, -0.35f, 0.35f), ctx.min.z - 400f + iz * wcell);
            for (int ix = 0; ix < wx; ix++)
                for (int iz = 0; iz < wz; iz++)
                {
                    water.TriFacing(rng.Next(2), wgrid[ix, iz], wgrid[ix, iz + 1], wgrid[ix + 1, iz + 1], Vector3.up);
                    water.TriFacing(rng.Next(2), wgrid[ix, iz], wgrid[ix + 1, iz + 1], wgrid[ix + 1, iz], Vector3.up);
                }
            var waterGo = CreateMeshObject("Water", parent, water.ToMesh("Water"), palette, "water", "waterB");
            waterGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // --- Dekorace podle tématu
        var scenery = new MeshBuilder(9);
        PlaceScenery(scenery, ctx, s, rng);
        var sceneryGo = CreateMeshObject("Scenery", parent, scenery.ToMesh("Scenery"), palette,
            "trunk", "leafA", "leafB", "rock", "leafC", "accentA", "accentB", "accentC", "metal");

        PlaceLandmarks(sceneryGo.transform, ctx, palette, rng);

        // --- Mraky (bez stínů)
        var clouds = new MeshBuilder(1);
        for (int i = 0; i < s.cloudCount; i++)
        {
            Vector3 c = new Vector3(Rand(rng, ctx.min.x, ctx.max.x), Rand(rng, 90f, 140f) + ctx.baseY, Rand(rng, ctx.min.z, ctx.max.z));
            int puffs = rng.Next(3, 6);
            for (int p = 0; p < puffs; p++)
            {
                Vector3 offset = new Vector3(p * 9f - puffs * 4.5f, (Rand(rng) - 0.5f) * 4f, (Rand(rng) - 0.5f) * 8f);
                clouds.Rock(0, c + offset, Rand(rng, 10f, 18f), rng);
            }
        }
        var cloudGo = CreateMeshObject("Clouds", parent, clouds.ToMesh("Clouds"), palette, "cloud");
        cloudGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void TerrainTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, TerrainContext ctx, System.Random rng)
    {
        float y = (a.y + b.y + c.y) / 3f;
        float h = y - ctx.baseY;
        float slope = Vector3.Angle(Vector3.Cross(b - a, c - a), Vector3.up);
        if (slope > 90f) slope = 180f - slope;

        int sub;
        if (ctx.theme.hasWater && y < ctx.waterLevel + 2.2f) sub = 5;       // pláž
        else if (h > ctx.theme.snowLine) sub = 4;                            // sníh na vrcholcích
        else if (h > ctx.theme.rockLine || slope > 36f) sub = 3;             // skály
        else if (ctx.theme.flatGround)                                       // město: dlažba, parky podle šumu
        {
            Vector3 mid = (a + b + c) / 3f;
            sub = Mathf.PerlinNoise(mid.x * 0.004f + 7f, mid.z * 0.004f + 3f) > 0.7f ? 2 : rng.Next(2);
        }
        else sub = rng.Next(3);                                              // tři odstíny trávy / písku
        mb.TriFacing(sub, a, b, c, Vector3.up);
    }

    // Submeshe "Scenery"
    private const int ScTrunk = 0, ScLeafA = 1, ScLeafB = 2, ScRock = 3, ScLeafC = 4, ScAccentA = 5, ScAccentB = 6, ScAccentC = 7, ScMetal = 8;

    private static bool RandomSpot(TerrainContext ctx, System.Random rng, float minDistance, float edgeMargin, out Vector3 spot)
    {
        float x = Rand(rng, ctx.min.x + edgeMargin, ctx.max.x - edgeMargin);
        float z = Rand(rng, ctx.min.z + edgeMargin, ctx.max.z - edgeMargin);
        float h = ctx.Height(x, z);
        spot = new Vector3(x, h, z);
        if (ctx.Distance(x, z) < minDistance) return false;
        if (ctx.theme.hasWater && h < ctx.waterLevel + 1f) return false;
        return true;
    }

    private static void PlaceScenery(MeshBuilder mb, TerrainContext ctx, TrackSettings s, System.Random rng)
    {
        TrackTheme.Scenery style = ctx.theme.scenery;
        int placed = 0, attempts = 0;
        while (placed < s.treeCount && attempts++ < s.treeCount * 20)
        {
            if (!RandomSpot(ctx, rng, ctx.flatZone + 2f, 40f, out Vector3 p)) continue;
            // Lesy/skupiny - hustota podle šumu
            if (style != TrackTheme.Scenery.City && Mathf.PerlinNoise(p.x * 0.012f + 50f, p.z * 0.012f + 50f) < 0.45f && Rand(rng) < 0.85f) continue;

            p.y -= 0.4f;
            float size = Rand(rng, 0.8f, 1.6f);
            switch (style)
            {
                case TrackTheme.Scenery.Desert:
                    if (Rand(rng) < 0.55f) Cactus(mb, p, size * 1.4f, rng);
                    else mb.Rock(ScLeafC, p + Vector3.up * 0.3f, Rand(rng, 0.8f, 1.8f), rng); // suchý keř
                    break;
                case TrackTheme.Scenery.Snow:
                    if (Rand(rng) < 0.85f) SnowPine(mb, p, size, rng);
                    else Pine(mb, p, size, rng);
                    break;
                case TrackTheme.Scenery.City:
                    if (Mathf.PerlinNoise(p.x * 0.004f + 7f, p.z * 0.004f + 3f) > 0.72f) RoundTree(mb, p, size, rng);
                    else continue;
                    break;
                case TrackTheme.Scenery.Coast:
                    if (Rand(rng) < 0.55f) Palm(mb, p, size, rng);
                    else RoundTree(mb, p, size, rng);
                    break;
                default:
                    if (Rand(rng) < 0.6f) Pine(mb, p, size, rng);
                    else if (Rand(rng) < 0.85f) RoundTree(mb, p, size, rng);
                    else HayBale(mb, p, rng);
                    break;
            }
            placed++;
        }

        for (int i = 0, tries = 0; i < s.rockCount && tries < s.rockCount * 20; tries++)
        {
            if (!RandomSpot(ctx, rng, ctx.flatZone, 30f, out Vector3 p)) continue;
            float size = Rand(rng, 1f, 4.5f);
            mb.Rock(ScRock, p + Vector3.up * size * 0.1f, size, rng);
            i++;
        }

        // Pobřeží: palmy a kameny přímo na pláži
        if (ctx.theme.hasWater)
        {
            for (int i = 0, tries = 0; i < 90 && tries < 4000; tries++)
            {
                float x = Rand(rng, ctx.min.x, ctx.max.x), z = Rand(rng, ctx.min.z, ctx.max.z);
                float h = ctx.Height(x, z);
                if (h < ctx.waterLevel + 0.3f || h > ctx.waterLevel + 2.2f || ctx.Distance(x, z) < ctx.flatZone) continue;
                if (Rand(rng) < 0.6f) Palm(mb, new Vector3(x, h - 0.3f, z), Rand(rng, 0.9f, 1.4f), rng);
                else mb.Rock(ScRock, new Vector3(x, h, z), Rand(rng, 1f, 2.5f), rng);
                i++;
            }
        }

        // Sněhuláci jako malé překvapení
        if (style == TrackTheme.Scenery.Snow)
        {
            for (int i = 0, tries = 0; i < 6 && tries < 500; tries++)
            {
                if (!RandomSpot(ctx, rng, ctx.flatZone + 1f, 60f, out Vector3 p) || ctx.Distance(p.x, p.z) > ctx.flatZone + 40f) continue;
                Snowman(mb, p, rng);
                i++;
            }
        }
    }

    /// <summary>Velké objekty, které dávají tratím charakter (stolové hory, větrné mlýny, maják).</summary>
    private static void PlaceLandmarks(Transform parent, TerrainContext ctx, IPalette palette, System.Random rng)
    {
        var mb = new MeshBuilder(9);
        switch (ctx.theme.scenery)
        {
            case TrackTheme.Scenery.Desert:
                for (int i = 0, tries = 0; i < 14 && tries < 800; tries++)
                {
                    float radius = Rand(rng, 18f, 40f);
                    if (!RandomSpot(ctx, rng, ctx.flatZone + radius + 12f, 60f, out Vector3 p)) continue;
                    Mesa(mb, p - Vector3.up * 3f, radius, Rand(rng, 28f, 62f), rng);
                    i++;
                }
                break;

            case TrackTheme.Scenery.Meadow:
                for (int i = 0, tries = 0; i < 4 && tries < 800; tries++)
                {
                    if (!RandomSpot(ctx, rng, ctx.flatZone + 25f, 80f, out Vector3 p) || ctx.Distance(p.x, p.z) > 160f) continue;
                    Windmill(mb, parent, p, ctx, palette, rng);
                    i++;
                }
                break;

            case TrackTheme.Scenery.Coast:
                // Maják na pobřeží nejblíž trati
                for (int tries = 0; tries < 3000; tries++)
                {
                    float x = Rand(rng, ctx.trackMaxX + 10f, ctx.trackMaxX + 90f), z = Rand(rng, ctx.min.z + 150f, ctx.max.z - 150f);
                    float h = ctx.Height(x, z);
                    if (h < ctx.waterLevel + 0.6f || h > ctx.waterLevel + 4f || ctx.Distance(x, z) < ctx.flatZone + 10f) continue;
                    Lighthouse(mb, parent, new Vector3(x, h - 0.5f, z), palette);
                    break;
                }
                break;

            case TrackTheme.Scenery.City:
                BuildCity(parent, ctx, palette, rng);
                break;

            case TrackTheme.Scenery.Snow:
                // Lyžařské chaty
                for (int i = 0, tries = 0; i < 5 && tries < 800; tries++)
                {
                    if (!RandomSpot(ctx, rng, ctx.flatZone + 12f, 80f, out Vector3 p) || ctx.Distance(p.x, p.z) > 90f) continue;
                    Cabin(mb, p, rng);
                    i++;
                }
                break;
        }
        CreateMeshObject("Landmarks", parent, mb.ToMesh("Landmarks"), palette,
            "trunk", "leafA", "leafB", "rock", "leafC", "accentA", "accentB", "accentC", "metal");
    }

    // ---------------------------------------------------------------- dekorace

    private static void Pine(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        mb.Cylinder(ScTrunk, p, 0.35f * size, 0.25f * size, 2.5f * size, 5);
        int leaf = Rand(rng) < 0.5f ? ScLeafA : ScLeafB;
        float angle = Rand(rng) * Mathf.PI;
        mb.Cylinder(leaf, p + Vector3.up * 1.8f * size, 2.4f * size, 0f, 3.2f * size, 7, angle);
        mb.Cylinder(leaf, p + Vector3.up * 3.4f * size, 1.9f * size, 0f, 2.8f * size, 7, angle + 0.3f);
        mb.Cylinder(leaf, p + Vector3.up * 4.9f * size, 1.3f * size, 0f, 2.4f * size, 7, angle + 0.6f);
    }

    private static void SnowPine(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        mb.Cylinder(ScTrunk, p, 0.3f * size, 0.22f * size, 2f * size, 5);
        float angle = Rand(rng) * Mathf.PI;
        mb.Cylinder(ScLeafA, p + Vector3.up * 1.4f * size, 2.3f * size, 0f, 3.4f * size, 6, angle);
        mb.Cylinder(ScLeafB, p + Vector3.up * 3.1f * size, 1.8f * size, 0f, 3.0f * size, 6, angle + 0.5f);
        // Sněhová čepice
        mb.Cylinder(ScLeafC, p + Vector3.up * 4.7f * size, 1.05f * size, 0f, 2.1f * size, 6, angle + 0.5f);
    }

    private static void RoundTree(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        mb.Cylinder(ScTrunk, p, 0.3f * size, 0.22f * size, 3f * size, 5);
        mb.Rock(Rand(rng) < 0.5f ? ScLeafB : ScLeafA, p + Vector3.up * 4.2f * size, 4f * size, rng);
    }

    private static void HayBale(MeshBuilder mb, Vector3 p, System.Random rng)
    {
        mb.Cylinder(ScAccentC, p + Vector3.up * 0.3f, 0.9f, 0.9f, 1.2f, 8, Rand(rng));
    }

    private static void Cactus(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        float h = 4f * size;
        int leaf = Rand(rng) < 0.5f ? ScLeafA : ScLeafB;
        mb.Cylinder(leaf, p, 0.45f * size, 0.38f * size, h, 6);
        mb.Cylinder(leaf, p + Vector3.up * h, 0.38f * size, 0.1f * size, 0.4f * size, 6);
        float angle = Rand(rng) * Mathf.PI * 2f;
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        int arms = rng.Next(1, 3);
        for (int a = 0; a < arms; a++)
        {
            Vector3 d = a == 0 ? dir : -dir;
            float armY = h * Rand(rng, 0.35f, 0.6f);
            Vector3 elbow = p + Vector3.up * armY + d * 1.0f * size;
            mb.Box(leaf, p + Vector3.up * armY + d * 0.5f * size, new Vector3(0.5f, 0.5f, 1.1f) * size, Quaternion.LookRotation(d, Vector3.up));
            mb.Cylinder(leaf, elbow, 0.3f * size, 0.26f * size, h * 0.35f, 6);
        }
    }

    private static void Palm(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        // Mírně prohnutý kmen z několika článků
        float lean = Rand(rng) * Mathf.PI * 2f;
        Vector3 leanDir = new Vector3(Mathf.Cos(lean), 0f, Mathf.Sin(lean));
        Vector3 pos = p;
        const int segments = 6;
        float segH = 1.2f * size;
        for (int i = 0; i < segments; i++)
        {
            float r = Mathf.Lerp(0.32f, 0.2f, i / (float)segments) * size;
            mb.Cylinder(ScTrunk, pos, r, r * 0.9f, segH, 5, i * 0.4f);
            pos += Vector3.up * segH + leanDir * (0.12f * i * size);
        }
        // Listy - oboustranné trojúhelníky do kruhu
        int leaves = 7;
        for (int i = 0; i < leaves; i++)
        {
            float a = Mathf.PI * 2f * i / leaves + Rand(rng) * 0.3f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = Vector3.Cross(Vector3.up, d) * 0.55f * size;
            Vector3 mid = pos + d * 1.8f * size + Vector3.up * 0.35f * size;
            Vector3 tip = pos + d * 3.6f * size - Vector3.up * 1.0f * size;
            int leaf = i % 2 == 0 ? ScLeafA : ScLeafB;
            mb.TriDoubleSided(leaf, pos, mid + side, mid - side);
            mb.TriDoubleSided(leaf, mid + side, tip, mid - side);
        }
        mb.Ball(ScTrunk, pos, 0.4f * size, 3, 5);
    }

    private static void Snowman(MeshBuilder mb, Vector3 p, System.Random rng)
    {
        mb.Ball(ScAccentA, p + Vector3.up * 0.8f, 1.0f, 4, 8);
        mb.Ball(ScAccentA, p + Vector3.up * 2.2f, 0.72f, 4, 8);
        mb.Ball(ScAccentA, p + Vector3.up * 3.25f, 0.5f, 4, 7);
        float a = Rand(rng) * Mathf.PI * 2f;
        Vector3 face = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        Quaternion rot = Quaternion.LookRotation(face, Vector3.up);
        // Mrkev: kužel natočený dopředu = krabička zužující se ke špičce
        Vector3 nose = p + Vector3.up * 3.25f + face * 0.45f;
        mb.TriOutward(ScAccentB, nose + Vector3.up * 0.08f, nose - Vector3.up * 0.08f + rot * new Vector3(0.08f, 0, 0), nose + face * 0.5f, nose);
        mb.TriOutward(ScAccentB, nose + Vector3.up * 0.08f, nose - Vector3.up * 0.08f - rot * new Vector3(0.08f, 0, 0), nose + face * 0.5f, nose);
        mb.TriOutward(ScAccentB, nose - Vector3.up * 0.08f + rot * new Vector3(0.08f, 0, 0), nose - Vector3.up * 0.08f - rot * new Vector3(0.08f, 0, 0), nose + face * 0.5f, nose);
        // Klobouk
        mb.Cylinder(ScAccentC, p + Vector3.up * 3.62f, 0.55f, 0.55f, 0.08f, 8);
        mb.Cylinder(ScAccentC, p + Vector3.up * 3.7f, 0.36f, 0.36f, 0.5f, 8);
    }

    private static void Mesa(MeshBuilder mb, Vector3 p, float radius, float height, System.Random rng)
    {
        // Vrstvená stolová hora - každá vrstva je trochu užší a jinak barevná
        int layers = rng.Next(3, 5);
        float y = 0f;
        int sides = rng.Next(6, 9);
        float angle = Rand(rng) * Mathf.PI;
        for (int i = 0; i < layers; i++)
        {
            float h = height / layers * Rand(rng, 0.8f, 1.2f);
            float r0 = radius * (1f - i * 0.08f);
            float r1 = radius * (1f - (i + 1) * 0.08f) * Rand(rng, 0.92f, 1f);
            int sub = i % 3 == 0 ? ScAccentA : (i % 3 == 1 ? ScAccentB : ScAccentC);
            mb.Cylinder(sub, p + Vector3.up * y, r0, r1, h, sides, angle + i * 0.15f);
            y += h;
        }
        // Balvany u úpatí
        for (int i = 0; i < 5; i++)
        {
            float a = Rand(rng) * Mathf.PI * 2f;
            mb.Rock(ScRock, p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * 1.05f + Vector3.up * 3f, Rand(rng, 3f, 7f), rng);
        }
    }

    private static void Windmill(MeshBuilder mb, Transform parent, Vector3 p, TerrainContext ctx, IPalette palette, System.Random rng)
    {
        const float towerH = 14f;
        mb.Cylinder(ScAccentA, p - Vector3.up * 0.5f, 3.2f, 2.0f, towerH, 8);
        mb.Cylinder(ScAccentB, p + Vector3.up * (towerH - 0.5f), 2.3f, 0f, 3.2f, 8);
        mb.Box(ScTrunk, p + Vector3.up * 1.2f + Vector3.forward * 2.9f, new Vector3(1.4f, 2.4f, 0.3f));

        // Lopatky jako samostatný objekt, který se točí
        Vector3 toTrack = Vector3.zero;
        NearestPoint(ctx.track, p.x, p.z, out Vector3 nearest);
        toTrack = nearest - p;
        toTrack.y = 0f;
        Quaternion face = Quaternion.LookRotation(toTrack.sqrMagnitude > 0.01f ? toTrack.normalized : Vector3.forward, Vector3.up);
        Vector3 hub = p + Vector3.up * (towerH - 1.2f) + face * Vector3.forward * 2.4f;

        var blades = new MeshBuilder(2);
        blades.Box(1, Vector3.zero, new Vector3(0.8f, 0.8f, 0.8f));
        for (int i = 0; i < 4; i++)
        {
            Quaternion r = Quaternion.Euler(0f, 0f, i * 90f);
            blades.Box(0, r * new Vector3(0f, 4.5f, 0f), new Vector3(0.25f, 9f, 0.15f), r);
            blades.Box(0, r * new Vector3(0.9f, 5.2f, 0f), new Vector3(1.6f, 7f, 0.06f), r);
        }
        var go = CreateMeshObject("WindmillBlades", parent, blades.ToMesh("Blades"), palette, "accentA", "trunk");
        go.isStatic = false;
        go.transform.SetPositionAndRotation(hub, face);
        var spin = go.AddComponent<Spinner>();
        spin.axis = Vector3.forward;
        spin.degreesPerSecond = Rand(rng, 25f, 45f);
    }

    private static void Lighthouse(MeshBuilder mb, Transform parent, Vector3 p, IPalette palette)
    {
        const int stripes = 6;
        const float stripeH = 3.2f;
        for (int i = 0; i < stripes; i++)
        {
            float r0 = Mathf.Lerp(3.2f, 2.0f, i / (float)stripes);
            float r1 = Mathf.Lerp(3.2f, 2.0f, (i + 1) / (float)stripes);
            mb.Cylinder(i % 2 == 0 ? ScAccentA : ScAccentB, p + Vector3.up * (i * stripeH), r0, r1, stripeH, 10);
        }
        float top = stripes * stripeH;
        mb.Cylinder(ScMetal, p + Vector3.up * top, 3f, 3f, 0.5f, 10);
        mb.Cylinder(ScAccentC, p + Vector3.up * (top + 0.5f), 1.6f, 1.6f, 2.4f, 8);
        mb.Cylinder(ScAccentB, p + Vector3.up * (top + 2.9f), 2f, 0f, 2f, 10);
        mb.Cylinder(ScRock, p - Vector3.up * 1.5f, 6f, 4.5f, 2f, 9);

        // Otáčející se paprsek světla
        var beam = new MeshBuilder(1);
        beam.Box(0, new Vector3(0f, 0f, 3.5f), new Vector3(0.6f, 0.6f, 7f));
        beam.Box(0, new Vector3(0f, 0f, -3.5f), new Vector3(0.6f, 0.6f, 7f));
        var go = CreateMeshObject("LighthouseBeam", parent, beam.ToMesh("Beam"), palette, "lamp");
        go.isStatic = false;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.position = p + Vector3.up * (top + 1.7f);
        var spin = go.AddComponent<Spinner>();
        spin.axis = Vector3.up;
        spin.degreesPerSecond = 60f;
    }

    private static void Cabin(MeshBuilder mb, Vector3 p, System.Random rng)
    {
        Quaternion rot = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
        mb.Box(ScTrunk, p + Vector3.up * 1.6f, new Vector3(6f, 3.6f, 5f), rot);
        // Sedlová střecha se sněhem
        Vector3 ridgeA = p + rot * new Vector3(0f, 5.6f, -2.9f), ridgeB = p + rot * new Vector3(0f, 5.6f, 2.9f);
        Vector3 l0 = p + rot * new Vector3(-3.6f, 3.2f, -2.9f), l1 = p + rot * new Vector3(-3.6f, 3.2f, 2.9f);
        Vector3 r0 = p + rot * new Vector3(3.6f, 3.2f, -2.9f), r1 = p + rot * new Vector3(3.6f, 3.2f, 2.9f);
        Vector3 center = p + Vector3.up * 4f;
        mb.QuadOutward(ScAccentA, l0, l1, ridgeB, ridgeA, center);
        mb.QuadOutward(ScAccentA, r0, r1, ridgeB, ridgeA, center);
        mb.TriOutward(ScTrunk, l0, r0, ridgeA, center);
        mb.TriOutward(ScTrunk, l1, r1, ridgeB, center);
        mb.Box(ScAccentC, p + rot * new Vector3(0f, 1.1f, 2.52f), new Vector3(1.2f, 2.2f, 0.1f), rot);
        mb.Box(ScAccentB, p + rot * new Vector3(1.8f, 2f, 2.52f), new Vector3(1.1f, 1f, 0.1f), rot);
        mb.Box(ScRock, p + rot * new Vector3(-2f, 6f, 0f), new Vector3(0.8f, 2.4f, 0.8f), rot);
    }

    // ---------------------------------------------------------------- noc

    /// <summary>Pouliční lampy podél trati se skutečnými světly (jen v noci).</summary>
    private static void BuildLamps(Transform parent, TrackResult t, TrackSettings s, IPalette palette)
    {
        var mb = new MeshBuilder(2);
        var root = new GameObject("Lamps").transform;
        root.SetParent(parent, false);
        int n = t.centers.Length;
        float offset = s.roadWidth * 0.5f + s.barrierOffset + 1.2f;
        const float height = 7.5f;
        for (int i = 0, k = 0; i < n; i += 9, k++)
        {
            float side = k % 2 == 0 ? 1f : -1f;
            Vector3 basePos = t.Center(i) + t.Right(i) * side * offset + Vector3.down * 0.4f;
            Vector3 toRoad = -t.Right(i) * side;
            mb.Cylinder(0, basePos, 0.14f, 0.1f, height, 6);
            Vector3 armEnd = basePos + Vector3.up * height + toRoad * 2.4f;
            mb.Box(0, (basePos + Vector3.up * height + armEnd) * 0.5f, new Vector3(0.12f, 0.12f, 2.4f), Quaternion.LookRotation(toRoad, Vector3.up));
            mb.Box(1, armEnd - Vector3.up * 0.12f, new Vector3(0.7f, 0.16f, 0.45f), Quaternion.LookRotation(toRoad, Vector3.up));

            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.position = armEnd - Vector3.up * 0.6f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 24f;
            light.intensity = 1.5f;
            light.color = new Color(1f, 0.82f, 0.55f);
            light.shadows = LightShadows.None;
        }
        var go = CreateMeshObject("LampPosts", root, mb.ToMesh("Lamps"), palette, "metal", "lamp");
        go.layer = 2;
        root.gameObject.AddComponent<LampCuller>();
    }

    /// <summary>Město: mrakodrapy s rozsvícenými okny, neonové reklamy, střechy.</summary>
    private static void BuildCity(Transform parent, TerrainContext ctx, IPalette palette, System.Random rng)
    {
        const int body0 = 0, windowA = 3, windowB = 4, windowC = 5, neonA = 6, neonB = 7, roof = 8;
        var mb = new MeshBuilder(9);
        const float cell = 30f;
        for (float x = ctx.min.x + 40f; x < ctx.max.x - 40f; x += cell)
        {
            for (float z = ctx.min.z + 40f; z < ctx.max.z - 40f; z += cell)
            {
                float px = x + Rand(rng, -4f, 4f), pz = z + Rand(rng, -4f, 4f);
                float w = Rand(rng, 12f, 22f), d = Rand(rng, 12f, 22f);
                float dist = ctx.Distance(px, pz);
                if (dist < ctx.flatZone + Mathf.Max(w, d) * 0.75f) continue;
                if (Mathf.PerlinNoise(px * 0.004f + 7f, pz * 0.004f + 3f) > 0.68f) continue; // park
                // Blíž k trati nižší domy, dál mrakodrapy
                float h = Rand(rng, 10f, 26f) + Mathf.Clamp01((dist - 40f) / 200f) * Rand(rng, 10f, 70f);
                Vector3 b = new Vector3(px, ctx.Height(px, pz) - 0.3f, pz);
                Building(mb, b, w, d, h, body0 + rng.Next(3), rng, windowA, windowB, windowC, neonA, neonB, roof);
            }
        }
        var go = CreateMeshObject("City", parent, mb.ToMesh("City"), palette,
            "accentA", "accentB", "accentC", "windowA", "windowB", "windowC", "neonA", "neonB", "roof");
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    private static void Building(MeshBuilder mb, Vector3 b, float w, float d, float h, int bodySub, System.Random rng,
        int windowA, int windowB, int windowC, int neonA, int neonB, int roof)
    {
        mb.Box(bodySub, b + Vector3.up * (h * 0.5f), new Vector3(w, h, d));
        mb.Box(roof, b + Vector3.up * (h + 0.3f), new Vector3(w - 1f, 0.6f, d - 1f));
        if (Rand(rng) < 0.6f) mb.Box(roof, b + new Vector3(Rand(rng, -w * 0.3f, w * 0.3f), h + 1.5f, Rand(rng, -d * 0.3f, d * 0.3f)), new Vector3(3f, 2f, 3f));

        int windowSub = Rand(rng) < 0.6f ? windowA : (Rand(rng) < 0.5f ? windowB : windowC);
        const float floor = 3.6f, spacing = 3.2f;
        float lit = Rand(rng, 0.35f, 0.75f);
        // Okna na všech čtyřech stěnách (jen ploché čtyřúhelníky, ať je to levné)
        for (int face = 0; face < 4; face++)
        {
            Vector3 normal = face == 0 ? Vector3.forward : face == 1 ? Vector3.back : face == 2 ? Vector3.right : Vector3.left;
            Vector3 along = face < 2 ? Vector3.right : Vector3.forward;
            float half = face < 2 ? w * 0.5f : d * 0.5f;
            float depth = face < 2 ? d * 0.5f : w * 0.5f;
            for (float y = 3f; y < h - 1.5f; y += floor)
            {
                for (float a = -half + 2f; a < half - 1.5f; a += spacing)
                {
                    if (Rand(rng) > lit) continue;
                    Vector3 c = b + Vector3.up * y + normal * (depth + 0.03f) + along * a;
                    Vector3 u = along * 0.8f, v = Vector3.up * 0.9f;
                    mb.QuadFacing(windowSub, c - u - v, c + u - v, c + u + v, c - u + v, normal);
                }
            }
        }
        // Neonová reklama na střeše nebo boku
        if (Rand(rng) < 0.35f)
        {
            int neon = Rand(rng) < 0.5f ? neonA : neonB;
            mb.Box(neon, b + Vector3.up * (h + 2.2f) + Vector3.forward * (d * 0.5f - 0.5f), new Vector3(w * 0.7f, 2.4f, 0.3f));
        }
    }

    // ---------------------------------------------------------------- pomocné

    private static void NearestOnTrack(TrackResult t, float x, float z, out float distance, out float roadY)
    {
        int i = NearestIndex(t, x, z, out float best);
        roadY = t.centers[i].y;
        distance = Mathf.Sqrt(best);
    }

    /// <summary>Index nejbližšího bodu trati - prohledává mřížku po prstencích od nejbližší buňky.</summary>
    private static int NearestIndex(TrackResult t, float x, float z, out float bestSqr)
    {
        bestSqr = float.MaxValue;
        int bestIndex = 0;
        Vector3[] c = t.centers;
        if (t.index == null)
        {
            for (int i = 0; i < c.Length; i++)
            {
                float d = (c[i].x - x) * (c[i].x - x) + (c[i].z - z) * (c[i].z - z);
                if (d < bestSqr) { bestSqr = d; bestIndex = i; }
            }
            return bestIndex;
        }

        int cx = TrackResult.Cell(x, t.indexMin.x), cz = TrackResult.Cell(z, t.indexMin.y);
        for (int r = 0; r < 200; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue; // jen okraj prstence
                    if (!t.index.TryGetValue(TrackResult.Key(cx + dx, cz + dz), out var list)) continue;
                    foreach (int i in list)
                    {
                        float d = (c[i].x - x) * (c[i].x - x) + (c[i].z - z) * (c[i].z - z);
                        if (d < bestSqr) { bestSqr = d; bestIndex = i; }
                    }
                }
            // Všechny buňky dalších prstenců jsou dál než r * velikost buňky
            float reach = r * TrackResult.IndexCell;
            if (bestSqr < float.MaxValue && bestSqr <= reach * reach) break;
        }
        return bestIndex;
    }

    private static void NearestPoint(TrackResult t, float x, float z, out Vector3 point)
    {
        point = t.centers[NearestIndex(t, x, z, out _)];
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, IPalette palette, params string[] materialKeys)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var materials = new Material[materialKeys.Length];
        for (int i = 0; i < materialKeys.Length; i++)
            materials[i] = palette.Get(materialKeys[i]);
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        go.isStatic = true;
        return go;
    }
}
