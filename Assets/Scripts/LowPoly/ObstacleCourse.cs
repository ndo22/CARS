using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Překážková dráha pro režim POLICE CHASE: vyvýšená silnice ze startu do cíle (ne okruh)
/// se skokankami přes mezery, poli beden a sudů, slalomem, zúženými úseky, pohyblivými zátarasy, boost pady
/// a dřevěnými zátarasy přes celou silnici, které se dají prorazit (bedny se rozbijí taky).
///
/// Dráha se nejdřív naplánuje (<see cref="Generate"/> - čistá logika bez Unity objektů, testovatelná)
/// a pak postaví (<see cref="Build"/>). Směr dráhy se drží v rozmezí ±85° od startu, takže se nikdy nezkříží.
/// </summary>
public static class ObstacleCourse
{
    public const float Step = 3f;
    public const float BaseHeight = 10f;   // výška silnice nad terénem
    public const float RoadWidth = 12f;

    public enum Feature { Normal, Start, Finish, Curve, Ramp, Gap, Landing, Obstacles, Narrow, Boost, Movers, Slalom, Roadblock }
    public enum ItemType { Crate, Barrel, Block, Cone, BoostPad, Slider, Barricade }

    public struct Item
    {
        public ItemType type;
        public Vector3 position;
        public float yaw;
        public float param;   // posuvný zátaras: rozkmit; boost: síla
    }

    /// <summary>Naplánovaná dráha.</summary>
    public class Plan
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> widths = new List<float>();
        public readonly List<bool> solid = new List<bool>();
        public readonly List<bool> barrier = new List<bool>();
        public readonly List<Feature> features = new List<Feature>();
        public readonly List<Item> items = new List<Item>();
        public readonly List<int> safePoints = new List<int>();
        public float[] distance;
        public int playerStart, policeStart, finishIndex;
        public int jumps;
        public float length;

        public int Count => points.Count;

        /// <summary>Délka, kterou hráč opravdu jede: od svého startu do cíle.</summary>
        public float RaceLength => length - (distance != null ? distance[playerStart] : 0f);

        public Vector3 Forward(int i)
        {
            Vector3 f = points[Mathf.Min(i + 1, Count - 1)] - points[Mathf.Max(i - 1, 0)];
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        }

        public Vector3 Right(int i) => Vector3.Cross(Vector3.up, Forward(i)).normalized;

        /// <summary>Nejbližší bod dráhy v okně kolem odhadu (pro sledování postupu auta).</summary>
        public int Nearest(Vector3 p, int guess, int back = 8, int ahead = 40)
        {
            int best = Mathf.Clamp(guess, 0, Count - 1);
            float bestD = float.MaxValue;
            for (int i = Mathf.Max(0, guess - back); i <= Mathf.Min(Count - 1, guess + ahead); i++)
            {
                float d = (points[i] - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// <summary>Poslední bezpečné místo pro návrat na dráhu, které leží nejvýš na indexu <paramref name="index"/>.</summary>
        public int SafeBefore(int index)
        {
            int result = playerStart;
            foreach (int s in safePoints)
                if (s <= index) result = s;
            return result;
        }
    }

    // ================================================================ plánování

    private class Builder
    {
        public Plan plan = new Plan();
        public Vector3 pos = new Vector3(0f, BaseHeight, 0f);
        public float yaw;
        public System.Random rng;

        public void Add(int steps, Feature f, float width, bool solid, bool barrier, float yawPerStep = 0f, float risePerStep = 0f)
        {
            for (int k = 0; k < steps; k++)
            {
                plan.points.Add(pos);
                plan.widths.Add(width);
                plan.solid.Add(solid);
                plan.barrier.Add(barrier);
                plan.features.Add(f);
                yaw += yawPerStep;
                float r = yaw * Mathf.Deg2Rad;
                pos += new Vector3(Mathf.Sin(r), 0f, Mathf.Cos(r)) * Step + Vector3.up * risePerStep;
            }
        }

        public float Rand(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
        public int Index => plan.points.Count;

        public void AddItem(ItemType type, int index, float lateral, float param = 0f, float yawOffset = 0f)
        {
            Vector3 p = plan.points[index];
            float yawHere = Mathf.Atan2(plan.Forward(index).x, plan.Forward(index).z) * Mathf.Rad2Deg;
            plan.items.Add(new Item { type = type, position = p + plan.Right(index) * lateral, yaw = yawHere + yawOffset, param = param });
        }
    }

    /// <summary>Naplánuje dráhu pro daný level (1..). Stejný level dá vždy stejnou dráhu.</summary>
    public static Plan Generate(int level)
    {
        level = Mathf.Max(1, level);
        float d = Mathf.Clamp01((level - 1) / 14f); // obtížnost 0..1
        var b = new Builder { rng = new System.Random(1000 + level * 37) };

        // Start: rovinka, hráč 30 m od začátku, policie za ním
        b.Add(Mathf.RoundToInt(80f / Step), Feature.Start, RoadWidth, true, true);
        b.plan.policeStart = 3;
        b.plan.playerStart = Mathf.RoundToInt(32f / Step);

        int segments = 5 + level;
        Feature last = Feature.Start;
        bool jumpDone = false;
        for (int n = 0; n < segments; n++)
        {
            Feature next = PickSegment(b, level, last, n, segments, jumpDone);
            switch (next)
            {
                case Feature.Curve: Curve(b, d, level); break;
                case Feature.Ramp: Jump(b, d, level); jumpDone = true; break;
                case Feature.Obstacles: Obstacles(b, d, level); break;
                case Feature.Narrow: b.Add(Mathf.RoundToInt(b.Rand(36f, 54f) / Step), Feature.Narrow, Mathf.Lerp(8f, 6.2f, d), true, false); break;
                case Feature.Boost: Boost(b); break;
                case Feature.Movers: Movers(b, d); break;
                case Feature.Slalom: Slalom(b, d); break;
                case Feature.Roadblock: Roadblock(b, level); break;
                default: Hill(b, d); break;
            }
            last = next;
        }

        // Cíl: rovinka, čára 15 m za začátkem, pak ještě místo na zastavení
        b.Add(Mathf.RoundToInt(15f / Step), Feature.Normal, RoadWidth, true, true);
        b.plan.finishIndex = b.Index;
        b.Add(Mathf.RoundToInt(45f / Step), Feature.Finish, RoadWidth, true, true);

        Finish(b.plan);
        return b.plan;
    }

    private static Feature PickSegment(Builder b, int level, Feature last, int n, int total, bool jumpDone)
    {
        // Každý level má aspoň jeden skok (nejpozději v polovině)
        if (!jumpDone && n >= total / 2) return Feature.Ramp;

        var options = new List<Feature> { Feature.Curve, Feature.Curve, Feature.Ramp, Feature.Obstacles, Feature.Normal, Feature.Boost };
        if (level >= 2) { options.Add(Feature.Slalom); options.Add(Feature.Roadblock); }
        if (level >= 3) { options.Add(Feature.Narrow); options.Add(Feature.Ramp); }
        if (level >= 5) { options.Add(Feature.Movers); options.Add(Feature.Obstacles); }
        if (level >= 8) { options.Add(Feature.Ramp); options.Add(Feature.Movers); }
        for (int guard = 0; guard < 20; guard++)
        {
            Feature f = options[b.rng.Next(options.Count)];
            if (f != last || f == Feature.Curve) return f;
        }
        return Feature.Curve;
    }

    private static void Curve(Builder b, float d, int level)
    {
        float radius = b.Rand(Mathf.Lerp(55f, 32f, d), Mathf.Lerp(80f, 50f, d));
        float angle = b.Rand(30f, Mathf.Lerp(70f, 100f, d));
        // Zatáčet tak, aby směr zůstal do ±85° od startu (dráha se pak nemůže zkřížit)
        float sign = b.yaw > 25f ? -1f : b.yaw < -25f ? 1f : (b.rng.NextDouble() < 0.5 ? -1f : 1f);
        angle = Mathf.Min(angle, 85f - sign * b.yaw);
        if (angle < 10f) { sign = -sign; angle = Mathf.Min(60f, 85f - sign * b.yaw); }
        float arc = angle * Mathf.Deg2Rad * radius;
        int steps = Mathf.Max(4, Mathf.RoundToInt(arc / Step));
        // Od 10. levelu mají některé zatáčky svodidla jen občas
        bool barrier = level < 10 || b.rng.NextDouble() < 0.5;
        b.Add(steps, Feature.Curve, RoadWidth, true, barrier, sign * angle / steps);
    }

    private static void Hill(Builder b, float d)
    {
        float len = b.Rand(45f, 75f);
        int steps = Mathf.RoundToInt(len / Step);
        float target = BaseHeight + b.Rand(-3f, 6f + d * 6f);
        float rise = (target - b.pos.y) / steps;
        rise = Mathf.Clamp(rise, -0.3f, 0.3f); // max ~10 % stoupání
        b.Add(steps, Feature.Normal, RoadWidth, true, false, 0f, rise);
    }

    private static void Jump(Builder b, float d, int level)
    {
        float gap = Mathf.Lerp(7f, 22f, d) + b.Rand(-1.5f, 2f);
        // Rozjezd (u dlouhých skoků s boost padem)
        int approach = Mathf.RoundToInt(42f / Step);
        int approachStart = b.Index;
        b.Add(approach, Feature.Normal, RoadWidth, true, true);
        if (gap > 14f) b.AddItem(ItemType.BoostPad, approachStart + 2, 0f, 10f);

        // Skokanka: 9 m stoupání o ~1.8 m
        const int rampSteps = 3;
        float rampRise = 1.6f + d * 0.5f;
        b.Add(rampSteps, Feature.Ramp, 10f, true, false, 0f, rampRise / rampSteps);
        // Mezera: dráha jen "pro AI", bez silnice; dopad o 0.8 m níž
        int gapSteps = Mathf.Max(2, Mathf.RoundToInt(gap / Step));
        b.Add(gapSteps, Feature.Gap, 10f, false, false, 0f, -0.8f / gapSteps);
        // Dopadová plocha klesá zpět
        b.Add(4, Feature.Landing, 11f, true, false, 0f, -(rampRise - 0.8f) / 4f);
        b.Add(4, Feature.Normal, RoadWidth, true, true);
        b.plan.jumps++;
    }

    private static void Obstacles(Builder b, float d, int level)
    {
        int steps = Mathf.RoundToInt(b.Rand(54f, 72f) / Step);
        int start = b.Index;
        b.Add(steps, Feature.Obstacles, RoadWidth, true, true);
        float[] lanes = { -4.2f, -1.4f, 1.4f, 4.2f };
        for (int i = start + 3; i < start + steps - 1; i += 3 + (d < 0.5f ? 1 : 0))
        {
            int free = b.rng.Next(lanes.Length);
            int blocked = 1 + b.rng.Next(1 + Mathf.RoundToInt(d * 2f)); // 1..3 zablokované pruhy
            for (int k = 0, placed = 0; k < lanes.Length && placed < blocked; k++)
            {
                int lane = (free + 1 + k) % lanes.Length;
                if (lane == free) continue;
                double r = b.rng.NextDouble();
                ItemType type = level >= 4 && r < 0.25 + d * 0.2 ? ItemType.Block : (r < 0.65 ? ItemType.Crate : ItemType.Barrel);
                b.AddItem(type, i, lanes[lane] + b.Rand(-0.4f, 0.4f), 0f, b.Rand(-15f, 15f));
                placed++;
            }
        }
    }

    private static void Slalom(Builder b, float d)
    {
        int steps = Mathf.RoundToInt(66f / Step);
        int start = b.Index;
        b.Add(steps, Feature.Slalom, RoadWidth, true, true);
        bool left = b.rng.NextDouble() < 0.5;
        for (int i = start + 2; i < start + steps - 1; i += 3)
        {
            // Řada kuželů přes půlku silnice - projíždí se střídavě vlevo a vpravo
            float side = left ? -1f : 1f;
            for (float x = 0.5f; x < RoadWidth * 0.5f - 0.6f; x += 1.3f)
                b.AddItem(ItemType.Cone, i, side * x);
            left = !left;
        }
    }

    /// <summary>Zátaras přes celou silnici ze tří dřevěných zábran - dá se prorazit, ale stojí to rychlost.</summary>
    private static void Roadblock(Builder b, int level)
    {
        int steps = Mathf.RoundToInt(45f / Step);
        int start = b.Index;
        b.Add(steps, Feature.Roadblock, RoadWidth, true, true);
        int at = start + steps / 2;
        // Od 7. levelu je jedna krajní zábrana betonová - musíš trefit dřevěnou (prostřední je vždy dřevěná kvůli policii)
        int solid = level >= 7 ? (b.rng.NextDouble() < 0.5 ? 0 : 2) : -1;
        for (int k = 0; k < 3; k++)
            b.AddItem(k == solid ? ItemType.Block : ItemType.Barricade, at, (k - 1) * BarricadeWidth);
    }

    public const float BarricadeWidth = 3.7f;

    private static void Boost(Builder b)
    {
        int start = b.Index;
        b.Add(Mathf.RoundToInt(36f / Step), Feature.Boost, RoadWidth, true, true);
        b.AddItem(ItemType.BoostPad, start + 4, 0f, 9f);
    }

    private static void Movers(Builder b, float d)
    {
        int steps = Mathf.RoundToInt(60f / Step);
        int start = b.Index;
        b.Add(steps, Feature.Movers, RoadWidth, true, true);
        int count = 2 + Mathf.RoundToInt(d * 2f);
        for (int k = 0; k < count; k++)
        {
            int i = start + 3 + k * (steps - 6) / Mathf.Max(1, count - 1);
            b.AddItem(ItemType.Slider, Mathf.Min(i, start + steps - 2), 0f, Mathf.Lerp(0.6f, 1.1f, d) * (k % 2 == 0 ? 1f : -1f));
        }
    }

    private static void Finish(Plan p)
    {
        p.distance = new float[p.Count];
        for (int i = 1; i < p.Count; i++)
            p.distance[i] = p.distance[i - 1] + Vector3.Distance(p.points[i - 1], p.points[i]);
        p.length = p.distance[p.finishIndex];

        // Bezpečná místa pro návrat: pevná silnice mimo skoky, ne přímo v poli překážek
        for (int i = p.playerStart; i < p.finishIndex; i += 6)
        {
            bool ok = true;
            for (int k = Mathf.Max(0, i - 2); k <= Mathf.Min(p.Count - 1, i + 2); k++)
            {
                Feature f = p.features[k];
                if (!p.solid[k] || f == Feature.Ramp || f == Feature.Landing || f == Feature.Gap || f == Feature.Movers || f == Feature.Roadblock) ok = false;
            }
            if (ok) p.safePoints.Add(i);
        }
    }

    // ================================================================ stavba

    private const int SubAsphalt = 0, SubAsphaltB = 1, SubLine = 2, SubSide = 3, SubRampA = 4, SubRampB = 5, SubCurbA = 6, SubCurbB = 7;

    /// <summary>Postaví dráhu, překážky, brány, cíl a krajinu kolem.</summary>
    public static void Build(Transform parent, Plan p, TrackTheme theme, TrackGenerator.IPalette palette, int seed, TrackGenerator.TrackSettings environment)
    {
        var rng = new System.Random(seed);
        BuildRoad(parent, p, palette, rng);
        BuildBarriersAndPillars(parent, p, palette);
        BuildGates(parent, p, palette);
        BuildItems(parent, p, palette);

        environment.groundDrop = BaseHeight;
        environment.roadWidth = RoadWidth;
        environment.barrierOffset = 2f;
        TrackGenerator.BuildEnvironment(parent, p.points.ToArray(), environment, theme, palette);
    }

    private static void BuildRoad(Transform parent, Plan p, TrackGenerator.IPalette palette, System.Random rng)
    {
        var mb = new MeshBuilder(8);
        const float thickness = 1.1f;
        for (int i = 0; i < p.Count - 1; i++)
        {
            if (!p.solid[i] || !p.solid[i + 1]) continue;
            Vector3 c0 = p.points[i], c1 = p.points[i + 1];
            Vector3 r0 = p.Right(i), r1 = p.Right(i + 1);
            float w0 = p.widths[i] * 0.5f, w1 = p.widths[i + 1] * 0.5f;
            Feature f = p.features[i];

            Vector3 l0 = c0 - r0 * w0, l1 = c1 - r1 * w1, q0 = c0 + r0 * w0, q1 = c1 + r1 * w1;
            int top = f == Feature.Ramp || f == Feature.Landing ? (i % 2 == 0 ? SubRampA : SubRampB) : (rng.NextDouble() < 0.5 ? SubAsphalt : SubAsphaltB);
            mb.QuadFacing(top, l0, l1, q1, q0, Vector3.up);

            // Bez svodidel: červenobílý okraj, ať je vidět hrana
            if (!p.barrier[i] && f != Feature.Ramp && f != Feature.Landing)
            {
                int curb = i % 2 == 0 ? SubCurbA : SubCurbB;
                Vector3 up = Vector3.up * 0.03f;
                mb.QuadFacing(curb, l0 + up, l1 + up, l1 + r1 * 0.8f + up, l0 + r0 * 0.8f + up, Vector3.up);
                mb.QuadFacing(curb, q0 - r0 * 0.8f + up, q1 - r1 * 0.8f + up, q1 + up, q0 + up, Vector3.up);
            }
            else if (f != Feature.Ramp && f != Feature.Landing && i % 3 == 0)
            {
                // Středová čára
                Vector3 up = Vector3.up * 0.02f;
                mb.QuadFacing(SubLine, c0 - r0 * 0.15f + up, c1 - r1 * 0.15f + up, c1 + r1 * 0.15f + up, c0 + r0 * 0.15f + up, Vector3.up);
            }

            // Boky a spodek desky
            Vector3 down = Vector3.down * thickness;
            mb.QuadFacing(SubSide, l0, l1, l1 + down, l0 + down, -r0);
            mb.QuadFacing(SubSide, q0, q1, q1 + down, q0 + down, r0);
            mb.QuadFacing(SubSide, l0 + down, l1 + down, q1 + down, q0 + down, Vector3.down);

            // Čela u mezer (a na začátku/konci)
            Vector3 fwd = p.Forward(i);
            if (i == 0 || !p.solid[i - 1]) mb.QuadFacing(SubSide, l0, q0, q0 + down, l0 + down, -fwd);
            if (i + 2 >= p.Count || !p.solid[i + 2]) mb.QuadFacing(SubSide, l1, q1, q1 + down, l1 + down, fwd);
        }
        // Stínování: boky a spodek desky tmavší, silnice u krajů trochu tmavší
        mb.ShadeVertices(0, v =>
        {
            int i = NearestBrute(p, v);
            float below = p.points[i].y - v.y;
            float k = below > 0.05f ? Mathf.Lerp(0.85f, 0.55f, Mathf.Clamp01(below / 1.1f)) : 1f;
            float lateral = Mathf.Abs(Vector3.Dot(v - p.points[i], p.Right(i)));
            k *= 1f - 0.14f * Mathf.Clamp01((lateral - (p.widths[i] * 0.5f - 1.6f)) / 1.6f);
            return new Color(k, k, k, 1f);
        });
        var go = Create("CourseRoad", parent, mb.ToMesh("CourseRoad"), palette,
            "asphaltA", "asphaltB", "line", "rock", "accentC", "checkerBlack", "curbA", "curbB");
        go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
    }

    private static void BuildBarriersAndPillars(Transform parent, Plan p, TrackGenerator.IPalette palette)
    {
        var walls = new MeshBuilder(2);
        var pillars = new MeshBuilder(1);
        const float h = 0.9f, t = 0.45f;
        for (int i = 0; i < p.Count - 1; i++)
        {
            if (p.solid[i] && p.solid[i + 1] && p.barrier[i] && p.barrier[i + 1])
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 r0 = p.Right(i) * side, r1 = p.Right(i + 1) * side;
                    float w0 = p.widths[i] * 0.5f, w1 = p.widths[i + 1] * 0.5f;
                    Vector3 a0 = p.points[i] + r0 * (w0 - t), a1 = p.points[i + 1] + r1 * (w1 - t);
                    Vector3 o0 = p.points[i] + r0 * w0, o1 = p.points[i + 1] + r1 * w1;
                    Vector3 up = Vector3.up * h;
                    int sub = (i / 2) % 2;
                    walls.QuadFacing(sub, a0, a1, a1 + up, a0 + up, -r0);
                    walls.QuadFacing(sub, a0 + up, a1 + up, o1 + up, o0 + up, Vector3.up);
                    walls.QuadFacing(sub, o0, o1, o1 + up, o0 + up, r0);
                }
            }
            // Pilíře pod silnicí
            if (p.solid[i] && i % 9 == 4)
            {
                // Terén u dráhy je BaseHeight pod silnicí - pilíř od spodku desky (1.1 m) až kousek do země
                float height = BaseHeight - 1.1f + 1f;
                foreach (float x in new[] { -0.3f, 0.3f })
                {
                    Vector3 c = p.points[i] + p.Right(i) * (p.widths[i] * x) + Vector3.down * (1.1f + height * 0.5f);
                    pillars.Box(0, c, new Vector3(1.4f, height, 1.4f), Quaternion.LookRotation(p.Forward(i), Vector3.up));
                }
            }
        }
        walls.ShadeVertices(0, v =>
        {
            float k = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01((v.y - p.points[NearestBrute(p, v)].y) / 0.9f));
            return new Color(k, k, k, 1f);
        });
        pillars.ShadeVertices(0, v =>
        {
            float k = Mathf.Lerp(0.5f, 0.95f, Mathf.Clamp01(1f - (p.points[NearestBrute(p, v)].y - v.y) / BaseHeight));
            return new Color(k, k, k, 1f);
        });
        var wallGo = Create("CourseBarriers", parent, walls.ToMesh("CourseBarriers"), palette, "barrierA", "barrierB");
        wallGo.AddComponent<MeshCollider>().sharedMesh = wallGo.GetComponent<MeshFilter>().sharedMesh;
        Create("CoursePillars", parent, pillars.ToMesh("CoursePillars"), palette, "metal");
    }

    private static void BuildGates(Transform parent, Plan p, TrackGenerator.IPalette palette)
    {
        var mb = new MeshBuilder(3);
        foreach (int index in new[] { p.playerStart + 2, p.finishIndex })
        {
            Vector3 c = p.points[index];
            Vector3 f = p.Forward(index), r = p.Right(index);
            Quaternion rot = Quaternion.LookRotation(f, Vector3.up);
            float hw = p.widths[index] * 0.5f;
            mb.Box(2, c + r * (hw + 0.6f) + Vector3.up * 3f, new Vector3(0.6f, 6f, 0.6f), rot);
            mb.Box(2, c - r * (hw + 0.6f) + Vector3.up * 3f, new Vector3(0.6f, 6f, 0.6f), rot);
            int cells = 16;
            float cw = (hw * 2f + 1.8f) / cells;
            for (int k = 0; k < cells; k++)
            {
                Vector3 pos = c + Vector3.up * 6f + r * (-hw - 0.9f + cw * (k + 0.5f));
                mb.Box(k % 2, pos + Vector3.up * 0.3f, new Vector3(cw, 0.6f, 0.5f), rot);
                mb.Box((k + 1) % 2, pos - Vector3.up * 0.3f, new Vector3(cw, 0.6f, 0.5f), rot);
            }
            // Šachovnice na silnici
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 2; z++)
                {
                    float cell = hw * 2f / 12f;
                    Vector3 o = c + r * (-hw + x * cell) + f * (z * cell - cell) + Vector3.up * 0.03f;
                    mb.QuadFacing((x + z) % 2, o, o + f * cell, o + f * cell + r * cell, o + r * cell, Vector3.up);
                }
        }
        var go = Create("CourseGates", parent, mb.ToMesh("CourseGates"), palette, "checkerWhite", "checkerBlack", "metal");
        go.layer = 2;

        // Cílová čára (trigger)
        var finish = new GameObject("FinishLine");
        finish.layer = 2;
        finish.transform.SetParent(parent, false);
        finish.transform.SetPositionAndRotation(p.points[p.finishIndex] + Vector3.up * 2f, Quaternion.LookRotation(p.Forward(p.finishIndex), Vector3.up));
        var box = finish.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(p.widths[p.finishIndex] + 4f, 8f, 2f);
        finish.AddComponent<FinishLine>();
    }

    private static readonly Dictionary<ItemType, Mesh> meshes = new Dictionary<ItemType, Mesh>();

    private static Mesh ItemMesh(ItemType type)
    {
        if (meshes.TryGetValue(type, out Mesh m) && m != null) return m;
        MeshBuilder mb;
        switch (type)
        {
            case ItemType.Crate:
                mb = new MeshBuilder(2);
                mb.Box(0, Vector3.zero, new Vector3(1.3f, 1.3f, 1.3f));
                mb.Box(1, Vector3.zero, new Vector3(1.34f, 0.18f, 1.34f));
                mb.Box(1, Vector3.zero, new Vector3(0.18f, 1.34f, 1.34f));
                break;
            case ItemType.Barrel:
                mb = new MeshBuilder(2);
                mb.Cylinder(0, Vector3.down * 0.55f, 0.45f, 0.45f, 1.1f, 9);
                mb.Cylinder(1, Vector3.down * 0.1f, 0.47f, 0.47f, 0.2f, 9);
                break;
            case ItemType.Block:
                mb = new MeshBuilder(2);
                mb.Box(0, Vector3.zero, new Vector3(2.8f, 1.1f, 0.9f));
                mb.Box(1, Vector3.up * 0.3f, new Vector3(2.84f, 0.2f, 0.94f));
                break;
            case ItemType.Cone:
                mb = new MeshBuilder(2);
                mb.Box(1, Vector3.down * 0.33f, new Vector3(0.55f, 0.06f, 0.55f));
                mb.Cylinder(0, Vector3.down * 0.3f, 0.24f, 0.03f, 0.7f, 7);
                break;
            case ItemType.Slider:
                mb = new MeshBuilder(2);
                mb.Box(0, Vector3.zero, new Vector3(3.6f, 1.2f, 1f));
                for (int k = -1; k <= 1; k++)
                    mb.Box(1, new Vector3(k * 1.2f, 0f, 0f), new Vector3(0.5f, 1.24f, 1.04f));
                break;
            default: // BoostPad - šipky
                mb = new MeshBuilder(2);
                mb.Box(1, Vector3.zero, new Vector3(4.2f, 0.06f, 6f));
                for (int k = 0; k < 3; k++)
                {
                    Vector3 o = new Vector3(0f, 0.05f, -1.8f + k * 1.8f);
                    mb.TriFacing(0, o + new Vector3(-1.6f, 0f, -0.3f), o + new Vector3(0f, 0f, 0.9f), o + new Vector3(1.6f, 0f, -0.3f), Vector3.up);
                }
                break;
        }
        m = mb.ToMesh(type.ToString());
        meshes[type] = m;
        return m;
    }

    private static void BuildItems(Transform parent, Plan p, TrackGenerator.IPalette palette)
    {
        var root = new GameObject("CourseItems").transform;
        root.SetParent(parent, false);
        foreach (Item item in p.items)
        {
            if (item.type == ItemType.Barricade)
            {
                BuildBarricade(root, item, palette);
                continue;
            }
            var go = new GameObject(item.type.ToString());
            go.transform.SetParent(root, false);
            Quaternion rot = Quaternion.Euler(0f, item.yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = ItemMesh(item.type);
            var r = go.AddComponent<MeshRenderer>();
            switch (item.type)
            {
                case ItemType.Crate:
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.66f, rot);
                    r.sharedMaterials = new[] { palette.Get("trunk"), palette.Get("checkerBlack") };
                    go.AddComponent<BoxCollider>().size = Vector3.one * 1.3f;
                    go.AddComponent<Rigidbody>().mass = 25f;
                    // Při prudkém nárazu se bedna rozpadne na prkýnka
                    var crate = go.AddComponent<Breakable>();
                    crate.kind = Breakable.Kind.Crate;
                    crate.breakSpeed = 9f;
                    break;
                case ItemType.Barrel:
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.56f, rot);
                    r.sharedMaterials = new[] { palette.Get("barrierA"), palette.Get("checkerWhite") };
                    var cap = go.AddComponent<CapsuleCollider>();
                    cap.radius = 0.45f;
                    cap.height = 1.1f;
                    go.AddComponent<Rigidbody>().mass = 18f;
                    break;
                case ItemType.Block:
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.56f, rot);
                    r.sharedMaterials = new[] { palette.Get("metal"), palette.Get("curbA") };
                    go.AddComponent<BoxCollider>().size = new Vector3(2.8f, 1.1f, 0.9f);
                    break;
                case ItemType.Cone:
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.37f, rot);
                    r.sharedMaterials = new[] { palette.Get("curbA"), palette.Get("checkerWhite") };
                    var cc = go.AddComponent<BoxCollider>();
                    cc.size = new Vector3(0.45f, 0.7f, 0.45f);
                    go.AddComponent<Rigidbody>().mass = 3f;
                    break;
                case ItemType.Slider:
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.62f, rot);
                    r.sharedMaterials = new[] { palette.Get("barrierA"), palette.Get("checkerWhite") };
                    go.AddComponent<BoxCollider>().size = new Vector3(3.6f, 1.2f, 1f);
                    var body = go.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    var osc = go.AddComponent<Oscillator>();
                    osc.axis = rot * Vector3.right;
                    osc.amplitude = RoadWidth * 0.5f - 2f;
                    osc.speed = Mathf.Abs(item.param);
                    osc.phase = item.param < 0f ? Mathf.PI : 0f;
                    break;
                case ItemType.BoostPad:
                    go.layer = 2;
                    go.transform.SetPositionAndRotation(item.position + Vector3.up * 0.04f, rot);
                    r.sharedMaterials = new[] { palette.Get("boost"), palette.Get("checkerBlack") };
                    var trigger = go.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(4.2f, 2f, 6f);
                    trigger.center = Vector3.up;
                    go.AddComponent<BoostPad>().strength = item.param;
                    break;
            }
        }
    }

    private static Mesh plankMesh, trestleMesh;

    /// <summary>Dřevěná zábrana: dvě pruhovaná prkna na dvou kozách. Všechny díly se rozletí najednou.</summary>
    private static void BuildBarricade(Transform root, Item item, TrackGenerator.IPalette palette)
    {
        float width = BarricadeWidth - 0.15f;
        if (plankMesh == null)
        {
            var mb = new MeshBuilder(2);
            mb.Box(0, Vector3.zero, new Vector3(1f, 0.3f, 0.1f));
            // Bílé šikmé pruhy (v jednotkách délky prkna, prkno se pak natáhne na šířku zábrany)
            for (int k = 0; k < 4; k++)
            {
                float x = -0.375f + k * 0.25f;
                mb.Box(1, new Vector3(x, 0f, 0f), new Vector3(0.09f, 0.31f, 0.11f));
            }
            plankMesh = mb.ToMesh("BarricadePlank");
            var tm = new MeshBuilder(1);
            // Koza: dvě šikmé nohy a příčka
            tm.Box(0, new Vector3(0f, 0.55f, 0.18f), new Vector3(0.1f, 1.15f, 0.1f), Quaternion.Euler(-12f, 0f, 0f));
            tm.Box(0, new Vector3(0f, 0.55f, -0.18f), new Vector3(0.1f, 1.15f, 0.1f), Quaternion.Euler(12f, 0f, 0f));
            tm.Box(0, new Vector3(0f, 0.45f, 0f), new Vector3(0.1f, 0.08f, 0.5f));
            tm.ShadeVertical(0, 0f, 1.1f, 0.65f);
            trestleMesh = tm.ToMesh("BarricadeTrestle");
        }
        Quaternion rot = Quaternion.Euler(0f, item.yaw, 0f);
        Vector3 right = rot * Vector3.right;
        var parts = new List<Breakable>();
        Material[] plankMats = { palette.Get("curbA"), palette.Get("checkerWhite") };
        foreach (float y in new[] { 0.6f, 1.0f })
        {
            Breakable plank = Breakable.Create("BarricadePlank", root, plankMesh, plankMats, item.position + Vector3.up * y, rot,
                new Vector3(width, 1f, 1f), new Vector3(1f, 0.3f, 0.4f), Vector3.zero, Breakable.Kind.Plank);
            parts.Add(plank);
        }
        foreach (float x in new[] { -0.42f, 0.42f })
        {
            Breakable trestle = Breakable.Create("BarricadeTrestle", root, trestleMesh, new[] { palette.Get("trunk") }, item.position + right * (x * width), rot,
                Vector3.one, new Vector3(0.12f, 1.1f, 0.5f), new Vector3(0f, 0.55f, 0f), Breakable.Kind.Plank);
            parts.Add(trestle);
        }
        Breakable[] group = parts.ToArray();
        foreach (Breakable part in group)
        {
            part.group = group;
            part.breakSpeed = 4f;
            part.keepMomentum = 0.82f;
            part.mass = 12f;
            part.localNormal = Vector3.forward;
            part.onRoadLifetime = 3f;
        }
    }

    /// <summary>Nejbližší bod dráhy (vodorovně) - pro stínování při stavbě.</summary>
    private static int NearestBrute(Plan p, Vector3 v)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < p.Count; i++)
        {
            float dx = p.points[i].x - v.x, dz = p.points[i].z - v.z;
            float d = dx * dx + dz * dz;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private static GameObject Create(string name, Transform parent, Mesh mesh, TrackGenerator.IPalette palette, params string[] keys)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var materials = new Material[keys.Length];
        for (int i = 0; i < keys.Length; i++) materials[i] = palette.Get(keys[i]);
        go.AddComponent<MeshRenderer>().sharedMaterials = materials;
        go.isStatic = true;
        return go;
    }
}
