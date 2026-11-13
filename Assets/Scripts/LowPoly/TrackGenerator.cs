using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedurálně postaví low-poly okruh: silnici, obrubníky, travnaté krajnice, svodidla,
/// startovní bránu, checkpointy, kopcovitý terén, stromy, kameny a mraky.
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
    }

    public class TrackResult
    {
        public Vector3[] centers;
        public Vector3[] forwards;
        public Vector3[] rights;
        public Vector3 Center(int i) => centers[Mod(i, centers.Length)];
        public Vector3 Forward(int i) => forwards[Mod(i, forwards.Length)];
        public Vector3 Right(int i) => rights[Mod(i, rights.Length)];
    }

    /// <summary>Materiály podle "barev" použité tratí.</summary>
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

    public static TrackResult Build(Transform parent, TrackSettings s, IPalette palette)
    {
        var rng = new System.Random(s.seed);
        TrackResult track = Sample(s);
        int n = track.centers.Length;

        // Zatáčky - kde se směr mění víc, dáme červenobílé obrubníky
        bool[] isCorner = new bool[n];
        for (int i = 0; i < n; i++)
        {
            float angle = Vector3.Angle(track.Forward(i - 2), track.Forward(i + 2));
            isCorner[i] = angle > 6f;
        }
        // Obrubník kousek před a za zatáčkou
        bool[] curb = new bool[n];
        for (int i = 0; i < n; i++)
            for (int k = -2; k <= 2; k++)
                if (isCorner[Mod(i + k, n)]) curb[i] = true;

        BuildRoad(parent, track, curb, s, palette, rng);
        BuildBarriers(parent, track, s, palette);
        BuildStartGate(parent, track, s, palette);
        BuildCheckpoints(parent, track, s);
        BuildTerrainAndScenery(parent, track, s, palette, rng);

        return track;
    }

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
        var result = new TrackResult
        {
            centers = new Vector3[n],
            forwards = new Vector3[n],
            rights = new Vector3[n],
        };
        for (int i = 0; i < n; i++)
            result.centers[i] = points[Mod(start + i, n)];
        for (int i = 0; i < n; i++)
        {
            Vector3 f = result.Center(i + 1) - result.Center(i - 1);
            f.y = 0f;
            f.Normalize();
            result.forwards[i] = f;
            result.rights[i] = Vector3.Cross(Vector3.up, f).normalized;
        }
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

    private const int SubAsphaltA = 0, SubAsphaltB = 1, SubWhite = 2, SubRed = 3, SubVergeA = 4, SubVergeB = 5;

    private static void BuildRoad(Transform parent, TrackResult t, bool[] curb, TrackSettings s, IPalette palette, System.Random rng)
    {
        var mb = new MeshBuilder(6);
        int n = t.centers.Length;
        float hw = s.roadWidth * 0.5f;
        const float curbWidth = 1.4f;
        float vergeEnd = hw + s.barrierOffset + 4f;

        for (int i = 0; i < n; i++)
        {
            Vector3 c0 = t.Center(i), c1 = t.Center(i + 1);
            Vector3 r0 = t.Right(i), r1 = t.Right(i + 1);

            // Asfalt - dva odstíny střídané náhodně pro "plochý" low-poly vzhled
            int asphalt = rng.NextDouble() < 0.5 ? SubAsphaltA : SubAsphaltB;
            Strip(mb, asphalt, c0, c1, r0, r1, -hw, hw, 0f);

            // Přerušovaná středová čára
            if (i % 3 == 0)
                Strip(mb, SubWhite, c0, c1, r0, r1, -0.15f, 0.15f, 0.02f);

            float edge = hw;
            if (curb[i])
            {
                int color = (i % 2 == 0) ? SubRed : SubWhite;
                Strip(mb, color, c0, c1, r0, r1, hw, hw + curbWidth, 0.04f);
                Strip(mb, color, c0, c1, r0, r1, -hw - curbWidth, -hw, 0.04f);
                edge = hw + curbWidth;
            }
            else
            {
                // Bílá krajní čára
                Strip(mb, SubWhite, c0, c1, r0, r1, hw - 0.5f, hw - 0.2f, 0.02f);
                Strip(mb, SubWhite, c0, c1, r0, r1, -hw + 0.2f, -hw + 0.5f, 0.02f);
            }

            // Travnatá krajnice až za svodidla (je po ní možné jet)
            int verge = rng.NextDouble() < 0.5 ? SubVergeA : SubVergeB;
            Strip(mb, verge, c0, c1, r0, r1, edge, vergeEnd, -0.03f);
            Strip(mb, verge, c0, c1, r0, r1, -vergeEnd, -edge, -0.03f);
        }

        var go = CreateMeshObject("Road", parent, mb.ToMesh("Road"),
            palette.Get("asphaltA"), palette.Get("asphaltB"), palette.Get("white"), palette.Get("red"), palette.Get("vergeA"), palette.Get("vergeB"));
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
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
                mb.QuadFacing(sub, in0, in1, in1 + up, in0 + up, towardsRoad);         // vnitřní stěna
                mb.QuadFacing(sub, in0 + up, in1 + up, out1 + up, out0 + up, Vector3.up); // horní hrana
                mb.QuadFacing(sub, out0, out1, out1 + up, out0 + up, -towardsRoad);    // vnější stěna
            }
        }

        var go = CreateMeshObject("Barriers", parent, mb.ToMesh("Barriers"), palette.Get("barrierA"), palette.Get("barrierB"));
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    // ---------------------------------------------------------------- start

    private static void BuildStartGate(Transform parent, TrackResult t, TrackSettings s, IPalette palette)
    {
        var mb = new MeshBuilder(3);
        Vector3 c = t.Center(0);
        Vector3 f = t.Forward(0), r = t.Right(0);
        float hw = s.roadWidth * 0.5f;

        // Šachovnice přes silnici
        const int cols = 14, rows = 2;
        float cell = s.roadWidth / cols;
        for (int x = 0; x < cols; x++)
            for (int z = 0; z < rows; z++)
            {
                int sub = (x + z) % 2 == 0 ? 0 : 1;
                Vector3 o = c + r * (-hw + x * cell) + f * (z * cell - cell) + Vector3.up * 0.03f;
                mb.QuadFacing(sub, o, o + f * cell, o + f * cell + r * cell, o + r * cell, Vector3.up);
            }

        // Brána: dva sloupy a nosník
        Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
        float pillarX = hw + 2.5f;
        const float gateHeight = 6.5f;
        mb.Box(2, c + r * pillarX + Vector3.up * (gateHeight * 0.5f - 0.3f), new Vector3(0.8f, gateHeight, 0.8f), rot);
        mb.Box(2, c - r * pillarX + Vector3.up * (gateHeight * 0.5f - 0.3f), new Vector3(0.8f, gateHeight, 0.8f), rot);
        Vector3 beam = c + Vector3.up * gateHeight;
        for (int x = 0; x < cols + 4; x++)
        {
            float w = (pillarX * 2f + 0.8f) / (cols + 4);
            Vector3 p = beam + r * (-pillarX - 0.4f + w * (x + 0.5f));
            mb.Box(x % 2, p, new Vector3(w, 1.2f, 0.6f), rot);
        }

        var go = CreateMeshObject("StartGate", parent, mb.ToMesh("StartGate"), palette.Get("checkerWhite"), palette.Get("checkerBlack"), palette.Get("metal"));
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

    // ---------------------------------------------------------------- checkpointy

    private static void BuildCheckpoints(Transform parent, TrackResult t, TrackSettings s)
    {
        var root = new GameObject("Checkpoints");
        root.transform.SetParent(parent, false);

        int n = t.centers.Length;
        int every = Mathf.Max(1, Mathf.RoundToInt(s.checkpointSpacing / s.segmentLength));
        int count = n / every;
        int layer = LayerMask.NameToLayer("Checkpoint");

        for (int k = 0; k < count; k++)
        {
            int i = k * every;
            var cp = new GameObject((k + 1).ToString());
            if (layer >= 0) cp.layer = layer;
            cp.transform.SetParent(root.transform, false);
            cp.transform.SetPositionAndRotation(t.Center(i) + Vector3.up * 3f, Quaternion.LookRotation(t.Forward(i), Vector3.up));
            var box = cp.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(s.roadWidth + s.barrierOffset * 2f, 8f, 2f);
        }
    }

    // ---------------------------------------------------------------- terén a okolí

    private const int SubGrassA = 0, SubGrassB = 1, SubGrassC = 2, SubRock = 3, SubSnow = 4;

    private static void BuildTerrainAndScenery(Transform parent, TrackResult t, TrackSettings s, IPalette palette, System.Random rng)
    {
        Vector3 min = t.centers[0], max = t.centers[0];
        foreach (var c in t.centers)
        {
            min = Vector3.Min(min, c);
            max = Vector3.Max(max, c);
        }
        const float margin = 260f, cell = 12f;
        min -= new Vector3(margin, 0, margin);
        max += new Vector3(margin, 0, margin);

        float flatZone = s.roadWidth * 0.5f + s.barrierOffset + 8f;
        float noiseOffset = s.seed % 1000;

        // Výška terénu v bodě: u trati rovina těsně pod silnicí, dál kopce, na okraji hory
        Func<float, float, float> height = (x, z) =>
        {
            NearestOnTrack(t, x, z, out float dist, out float roadY);
            float hills = (Mathf.PerlinNoise(x * 0.007f + noiseOffset, z * 0.007f + noiseOffset) - 0.4f) * 40f
                        + (Mathf.PerlinNoise(x * 0.02f + noiseOffset, z * 0.02f) - 0.5f) * 8f;
            float edge = Mathf.Min(Mathf.Min(x - min.x, max.x - x), Mathf.Min(z - min.z, max.z - z));
            hills += Mathf.Max(0f, 120f - edge) * 0.45f; // hory na okraji mapy
            float blend = Mathf.SmoothStep(0f, 1f, (dist - flatZone) / 80f);
            return Mathf.Lerp(roadY - 0.4f, Mathf.Max(roadY - 6f, hills), blend);
        };

        int nx = Mathf.CeilToInt((max.x - min.x) / cell) + 1;
        int nz = Mathf.CeilToInt((max.z - min.z) / cell) + 1;
        var grid = new Vector3[nx, nz];
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                float x = min.x + ix * cell, z = min.z + iz * cell;
                float h = height(x, z);
                NearestOnTrack(t, x, z, out float dist, out _);
                if (dist > flatZone + 15f)
                {
                    // Trochu náhodného posunu - typický low-poly "zubatý" terén
                    x += ((float)rng.NextDouble() - 0.5f) * cell * 0.4f;
                    z += ((float)rng.NextDouble() - 0.5f) * cell * 0.4f;
                    h += ((float)rng.NextDouble() - 0.5f) * 1.2f;
                }
                grid[ix, iz] = new Vector3(x, h, z);
            }

        var terrain = new MeshBuilder(5);
        float baseY = t.centers[0].y;
        for (int ix = 0; ix < nx - 1; ix++)
            for (int iz = 0; iz < nz - 1; iz++)
            {
                Vector3 a = grid[ix, iz], b = grid[ix, iz + 1], c = grid[ix + 1, iz + 1], d = grid[ix + 1, iz];
                TerrainTri(terrain, a, b, c, baseY, rng);
                TerrainTri(terrain, a, c, d, baseY, rng);
            }
        var terrainGo = CreateMeshObject("Terrain", parent, terrain.ToMesh("Terrain"),
            palette.Get("grassA"), palette.Get("grassB"), palette.Get("grassC"), palette.Get("rock"), palette.Get("snow"));
        terrainGo.AddComponent<MeshCollider>().sharedMesh = terrainGo.GetComponent<MeshFilter>().sharedMesh;

        // Stromy a kameny
        var scenery = new MeshBuilder(5);
        int placed = 0, attempts = 0;
        while (placed < s.treeCount && attempts++ < s.treeCount * 20)
        {
            float x = Mathf.Lerp(min.x + 40f, max.x - 40f, (float)rng.NextDouble());
            float z = Mathf.Lerp(min.z + 40f, max.z - 40f, (float)rng.NextDouble());
            NearestOnTrack(t, x, z, out float dist, out _);
            if (dist < flatZone + 2f) continue;
            // Lesy - hustota podle šumu
            if (Mathf.PerlinNoise(x * 0.012f + 50f, z * 0.012f + 50f) < 0.45f && rng.NextDouble() < 0.85) continue;

            Vector3 basePos = new Vector3(x, height(x, z) - 0.4f, z);
            float size = Mathf.Lerp(0.8f, 1.6f, (float)rng.NextDouble());
            if (rng.NextDouble() < 0.65)
                Pine(scenery, basePos, size, rng);
            else
                RoundTree(scenery, basePos, size, rng);
            placed++;
        }

        for (int i = 0, tries = 0; i < s.rockCount && tries < s.rockCount * 20; tries++)
        {
            float x = Mathf.Lerp(min.x + 30f, max.x - 30f, (float)rng.NextDouble());
            float z = Mathf.Lerp(min.z + 30f, max.z - 30f, (float)rng.NextDouble());
            NearestOnTrack(t, x, z, out float dist, out _);
            if (dist < flatZone) continue;
            float size = Mathf.Lerp(1f, 4.5f, (float)rng.NextDouble());
            scenery.Rock(3, new Vector3(x, height(x, z) + size * 0.1f, z), size, rng);
            i++;
        }

        CreateMeshObject("Scenery", parent, scenery.ToMesh("Scenery"),
            palette.Get("trunk"), palette.Get("leafA"), palette.Get("leafB"), palette.Get("rock"), palette.Get("leafC"));

        // Mraky (bez stínů)
        var clouds = new MeshBuilder(1);
        for (int i = 0; i < s.cloudCount; i++)
        {
            Vector3 c = new Vector3(
                Mathf.Lerp(min.x, max.x, (float)rng.NextDouble()),
                Mathf.Lerp(90f, 140f, (float)rng.NextDouble()),
                Mathf.Lerp(min.z, max.z, (float)rng.NextDouble()));
            int puffs = rng.Next(3, 6);
            for (int p = 0; p < puffs; p++)
            {
                Vector3 offset = new Vector3(p * 9f - puffs * 4.5f, ((float)rng.NextDouble() - 0.5f) * 4f, ((float)rng.NextDouble() - 0.5f) * 8f);
                clouds.Rock(0, c + offset, Mathf.Lerp(10f, 18f, (float)rng.NextDouble()), rng);
            }
        }
        var cloudGo = CreateMeshObject("Clouds", parent, clouds.ToMesh("Clouds"), palette.Get("cloud"));
        cloudGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void TerrainTri(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, float baseY, System.Random rng)
    {
        float h = (a.y + b.y + c.y) / 3f - baseY;
        float slope = Vector3.Angle(Vector3.Cross(b - a, c - a), Vector3.up);
        if (slope > 90f) slope = 180f - slope;

        int sub;
        if (h > 55f) sub = SubSnow;
        else if (h > 30f || slope > 38f) sub = SubRock;
        else sub = rng.Next(3); // tři odstíny trávy
        mb.TriFacing(sub, a, b, c, Vector3.up);
    }

    private static void Pine(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        mb.Cylinder(0, p, 0.35f * size, 0.25f * size, 2.5f * size, 5);
        int leaf = rng.NextDouble() < 0.5 ? 1 : 4;
        float angle = (float)rng.NextDouble() * Mathf.PI;
        mb.Cylinder(leaf, p + Vector3.up * 1.8f * size, 2.4f * size, 0f, 3.2f * size, 7, angle);
        mb.Cylinder(leaf, p + Vector3.up * 3.4f * size, 1.9f * size, 0f, 2.8f * size, 7, angle + 0.3f);
        mb.Cylinder(leaf, p + Vector3.up * 4.9f * size, 1.3f * size, 0f, 2.4f * size, 7, angle + 0.6f);
    }

    private static void RoundTree(MeshBuilder mb, Vector3 p, float size, System.Random rng)
    {
        mb.Cylinder(0, p, 0.3f * size, 0.22f * size, 3f * size, 5);
        mb.Rock(2, p + Vector3.up * 4.2f * size, 4f * size, rng);
    }

    private static void NearestOnTrack(TrackResult t, float x, float z, out float distance, out float roadY)
    {
        float best = float.MaxValue;
        roadY = 0f;
        Vector3[] c = t.centers;
        for (int i = 0; i < c.Length; i++)
        {
            float dx = c[i].x - x, dz = c[i].z - z;
            float d = dx * dx + dz * dz;
            if (d < best)
            {
                best = d;
                roadY = c[i].y;
            }
        }
        distance = Mathf.Sqrt(best);
    }

    private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        go.isStatic = true;
        return go;
    }
}
