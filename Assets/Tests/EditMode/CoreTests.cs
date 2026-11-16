using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Testy čisté logiky - běží v Unity (Test Runner, EditMode) i v CI bez Unity (Tools/ci).</summary>
public class TimeFormatTests
{
    [Test]
    public void FormatRoundsToMilliseconds()
    {
        Assert.AreEqual("1:05.123", TimeFormat.Format(65.1234f));
        Assert.AreEqual("1:00.000", TimeFormat.Format(59.9996f)); // nikdy "0:60.000"
        Assert.AreEqual("--:--.---", TimeFormat.Format(float.PositiveInfinity));
    }

    [Test]
    public void RacingFormatUsesApostrophes()
    {
        Assert.AreEqual("1'05\"123", TimeFormat.FormatRacing(65.1234f));
        Assert.AreEqual("-'--\"---", TimeFormat.FormatRacing(-1f));
    }

    [Test]
    public void ParseAcceptsCommaAndDot()
    {
        Assert.IsTrue(TimeFormat.TryParse("1:05,123", out float a));
        Assert.AreEqual(65.123f, a, 0.001f);
        Assert.IsTrue(TimeFormat.TryParse("0:59.500", out float b));
        Assert.AreEqual(59.5f, b, 0.001f);
        Assert.IsFalse(TimeFormat.TryParse("bad", out _));
    }
}

public class TrackTests
{
    [Test]
    public void AllBuiltInTracksAreDrivable()
    {
        foreach (TrackLibrary.TrackDefinition def in TrackLibrary.All)
        {
            TrackGenerator.Validation v = TrackGenerator.Validate(TrackLibrary.Settings(def));
            Assert.IsTrue(v.ok, def.name + ": " + v.message);
            Assert.Greater(v.length, 900f, def.name + " is too short");
            Assert.Less(v.length, 2500f, def.name + " is too long");
        }
    }

    [Test]
    public void DefaultCustomTrackIsValid()
    {
        var s = new TrackGenerator.TrackSettings { controlPoints = CustomTracks.DefaultPoints(), heightScale = 1f };
        TrackGenerator.Validation v = TrackGenerator.Validate(s);
        Assert.IsTrue(v.ok, v.message);
    }

    [Test]
    public void SelfCrossingTrackIsRejected()
    {
        var points = new[]
        {
            new Vector3(0, 0, 0), new Vector3(0, 0, 200), new Vector3(200, 0, 0), new Vector3(200, 0, 200),
        };
        var v = TrackGenerator.Validate(new TrackGenerator.TrackSettings { controlPoints = points, heightScale = 1f });
        Assert.IsFalse(v.ok);
    }

    [Test]
    public void NormalizeMovesStartToOrigin()
    {
        Vector3[] p = CustomTracks.Normalize(new[] { new Vector3(10, 2, 20), new Vector3(30, 0, 50) });
        Assert.AreEqual(0f, p[0].x, 0.0001f);
        Assert.AreEqual(0f, p[0].z, 0.0001f);
        Assert.AreEqual(2f, p[0].y, 0.0001f); // výška zůstává
        Assert.AreEqual(20f, p[1].x, 0.0001f);
    }

    [Test]
    public void StartIsTheSampleClosestToOrigin()
    {
        foreach (TrackLibrary.TrackDefinition def in TrackLibrary.All)
        {
            Vector3[] path = TrackGenerator.SamplePath(TrackLibrary.Settings(def));
            float start = new Vector2(path[0].x, path[0].z).magnitude;
            foreach (Vector3 p in path)
                Assert.GreaterOrEqual(new Vector2(p.x, p.z).magnitude + 0.001f, start, def.name);
            Assert.Less(start, 15f, def.name + " should pass near the origin");
        }
    }
}

public class ChampionshipTests
{
    [Test]
    public void PointsAreAwardedAndSorted()
    {
        Championship.Start();
        Championship.AddResult("YOU", 2);
        Championship.AddResult("VIPER", 1);
        Championship.AddResult("NOVA", 7); // mimo body
        Championship.AddResult("YOU", 1);
        List<KeyValuePair<string, int>> sorted = Championship.Sorted();
        Assert.AreEqual("YOU", sorted[0].Key);
        Assert.AreEqual(16, sorted[0].Value);
        Assert.AreEqual(10, sorted[1].Value);
        Assert.AreEqual(0, sorted[2].Value);
        Championship.active = false;
    }
}

public class CarTests
{
    [Test]
    public void StatsAreNormalized()
    {
        foreach (LowPolyCarFactory.BodyStyle style in System.Enum.GetValues(typeof(LowPolyCarFactory.BodyStyle)))
        {
            LowPolyCarFactory.CarStats s = LowPolyCarFactory.Stats(style);
            foreach (float v in new[] { s.Speed, s.Accel, s.Grip, s.Handling })
            {
                Assert.GreaterOrEqual(v, 0f, style.ToString());
                Assert.LessOrEqual(v, 1f, style.ToString());
            }
            Assert.Greater(s.mass, 0f);
        }
    }

    [Test]
    public void EveryStyleHasAName()
    {
        Assert.AreEqual(System.Enum.GetValues(typeof(LowPolyCarFactory.BodyStyle)).Length, LowPolyCarFactory.StyleNames.Length);
    }
}

public class MeshBuilderTests
{
    [Test]
    public void BoxFacesPointOutwards()
    {
        var mb = new MeshBuilder(1);
        mb.Box(0, Vector3.zero, new Vector3(2f, 1f, 3f), Quaternion.Euler(10f, 30f, 5f));
        Mesh mesh = mb.ToMesh("Test");
        Vector3[] v = mesh.vertices;
        int[] t = mesh.GetTriangles(0);
        Assert.AreEqual(36, t.Length);
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Assert.Greater(Vector3.Dot(normal, (a + b + c) / 3f), 0f, "triangle " + i / 3 + " faces inwards");
        }
    }
}

public class ShadingTests
{
    [Test]
    public void VerticalShadingDarkensTheBottom()
    {
        var mb = new MeshBuilder(1);
        mb.Box(0, Vector3.up * 1f, new Vector3(1f, 2f, 1f), Quaternion.identity);
        mb.ShadeVertical(0, 0f, 2f, 0.5f);
        Mesh mesh = mb.ToMesh("Shaded");
        Vector3[] v = mesh.vertices;
        Color[] c = mesh.colors;
        Assert.AreEqual(v.Length, c.Length);
        for (int i = 0; i < v.Length; i++)
        {
            float expected = Mathf.Lerp(0.5f, 1f, v[i].y / 2f);
            Assert.AreEqual(expected, c[i].r, 0.001f, "vertex " + i);
        }
    }
}

public class GhostTests
{
    [Test]
    public void SaveAndLoadRoundTrip()
    {
        var lap = new GhostData.Lap { time = 61.5f, style = 2 };
        for (int i = 0; i < 20; i++)
        {
            lap.positions.Add(new Vector3(i, 0.5f, i * 2f));
            lap.rotations.Add(Quaternion.Euler(0f, i * 10f, 0f));
        }
        const string id = "__test_ghost";
        GhostData.Save(id, lap);
        GhostData.Lap loaded = GhostData.Load(id);
        Assert.IsNotNull(loaded);
        Assert.AreEqual(61.5f, loaded.time, 0.0001f);
        Assert.AreEqual(2, loaded.style);
        Assert.AreEqual(20, loaded.positions.Count);
        Assert.AreEqual(38f, loaded.positions[19].z, 0.0001f);
        System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, "ghost_" + id + ".bin"));
    }
}

public class AudioTests
{
    [Test]
    public void SongIsBoundedAndLoopsCleanly()
    {
        float[] data = ProceduralAudio.SongData(120f, new[] { 0, -5 }, true);
        Assert.Greater(data.Length, 44100 * 3);
        float max = 0f;
        foreach (float s in data) max = Mathf.Max(max, Mathf.Abs(s));
        Assert.LessOrEqual(max, 1f);
        Assert.AreEqual(0f, data[0], 0.0001f);
        Assert.AreEqual(0f, data[data.Length - 1], 0.0001f);
    }
}

public class MultiplayerTests
{
    [Test]
    public void ViewportsTileTheScreenWithoutOverlap()
    {
        for (int count = 2; count <= Multiplayer.MaxPlayers; count++)
        {
            float area = 0f;
            for (int i = 0; i < count; i++)
            {
                Rect a = Multiplayer.Viewport(i, count);
                Assert.IsTrue(a.xMin >= 0f && a.yMin >= 0f && a.xMax <= 1f && a.yMax <= 1f, "viewport outside the screen");
                area += a.width * a.height;
                for (int j = i + 1; j < count; j++)
                {
                    Rect b = Multiplayer.Viewport(j, count);
                    bool overlap = a.xMin < b.xMax - 0.001f && b.xMin < a.xMax - 0.001f && a.yMin < b.yMax - 0.001f && b.yMin < a.yMax - 0.001f;
                    Assert.IsFalse(overlap, count + " players: " + i + " overlaps " + j);
                }
            }
            // U 3 hráčů zbývá čtvrtina na přehledovou kameru
            Assert.AreEqual(count == 3 ? 0.75f : 1f, area, 0.001f);
        }
        // Hráč 1 je vždy nahoře vlevo
        Assert.AreEqual(1f, Multiplayer.Viewport(0, 4).yMax, 0.001f);
        Assert.AreEqual(0f, Multiplayer.Viewport(0, 4).xMin, 0.001f);
    }

    [Test]
    public void TheLastCarStillRacingIsEliminated()
    {
        var progress = new List<float> { 5f, 2f, 7f, 1f };
        var racing = new List<bool> { true, true, true, false };
        Assert.AreEqual(1, Multiplayer.PickEliminated(progress, racing));
        racing = new List<bool> { false, false, true, false };
        Assert.AreEqual(-1, Multiplayer.PickEliminated(progress, racing), "the last one left is never eliminated");
    }

    [Test]
    public void OneEliminationPerLapUntilOneIsLeft()
    {
        Assert.AreEqual(0, Multiplayer.EliminationsDue(0, 4));
        Assert.AreEqual(2, Multiplayer.EliminationsDue(2, 4));
        Assert.AreEqual(3, Multiplayer.EliminationsDue(9, 4));
        Assert.AreEqual(1, Multiplayer.EliminationsDue(5, 2));
    }

    [Test]
    public void PlayersNeverShareAColor()
    {
        int[] saved = (int[])Multiplayer.colors.Clone();
        try
        {
            for (int i = 0; i < 4; i++) Multiplayer.colors[i] = i;
            for (int step = 0; step < 20; step++)
            {
                int player = step % 4;
                Multiplayer.colors[player] = Multiplayer.NextFreeColor(player, 8, 4);
                for (int a = 0; a < 4; a++)
                    for (int b = a + 1; b < 4; b++)
                        Assert.AreNotEqual(Multiplayer.colors[a], Multiplayer.colors[b]);
            }
        }
        finally
        {
            for (int i = 0; i < saved.Length; i++) Multiplayer.colors[i] = saved[i];
        }
    }

    [Test]
    public void MatchScoreCountsWins()
    {
        Multiplayer.ResetMatch();
        Multiplayer.RecordWin(1);
        Multiplayer.RecordWin(1);
        Multiplayer.RecordWin(-1); // vyhrál počítač
        Assert.AreEqual(3, Multiplayer.roundsPlayed);
        Assert.AreEqual("P1 0   P2 2   P3 0", Multiplayer.MatchScore(3));
        Multiplayer.ResetMatch();
    }
}

public class BreakableTests
{
    [Test]
    public void BreakableBarriersAreOnTheOutsideOfCorners()
    {
        for (int t = 0; t < TrackLibrary.All.Length; t++)
        {
            TrackGenerator.TrackResult track = TrackGenerator.SampleTrack(TrackLibrary.Settings(TrackLibrary.All[t]));
            int[] sides = TrackGenerator.BreakableBarrierSides(track);
            int n = sides.Length, count = 0;
            for (int i = 0; i < n; i++)
            {
                if (sides[i] == 0) continue;
                count++;
                Assert.IsTrue(i > 8 && i < n - 8, TrackLibrary.All[t].name + ": breakable barrier next to the start");
            }
            Assert.Greater(count, 0, TrackLibrary.All[t].name + ": no breakable barriers");
            Assert.Less(count, n * 0.4f, TrackLibrary.All[t].name + ": too many breakable pieces");

            // Zničitelné svodidlo je na vnější straně zatáčky, kterou auto právě projelo
            for (int i = 0; i < n; i++)
            {
                if (sides[i] == 0) continue;
                Vector3 before = track.Forward(i - 5), after = track.Forward(i + 1);
                if (Vector3.Angle(before, after) < 8f) continue;
                int outer = Vector3.Cross(before, after).y > 0f ? -1 : 1;
                Assert.AreEqual(outer, sides[i], TrackLibrary.All[t].name + " segment " + i);
            }
        }
    }

    [Test]
    public void RoadblocksSpanTheRoadWithAWoodenMiddle()
    {
        int roadblocks = 0;
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            for (int k = 0; k + 2 < p.items.Count; k++)
            {
                ObstacleCourse.Item a = p.items[k], b = p.items[k + 1], c = p.items[k + 2];
                bool isBlock = (a.type == ObstacleCourse.ItemType.Barricade || a.type == ObstacleCourse.ItemType.Block)
                    && b.type == ObstacleCourse.ItemType.Barricade
                    && (c.type == ObstacleCourse.ItemType.Barricade || c.type == ObstacleCourse.ItemType.Block)
                    && Vector3.Distance(a.position, c.position) > 7f;
                if (!isBlock) continue;
                roadblocks++;
                // Prostřední zábrana je vždy dřevěná (policie jede středem), betonová jen od 7. levelu
                if (level < 7)
                {
                    Assert.AreEqual(ObstacleCourse.ItemType.Barricade, a.type);
                    Assert.AreEqual(ObstacleCourse.ItemType.Barricade, c.type);
                }
                Assert.IsFalse(a.type == ObstacleCourse.ItemType.Block && c.type == ObstacleCourse.ItemType.Block, "level " + level);
                k += 2;
            }
        }
        Assert.Greater(roadblocks, 0);
    }
}

public class ChaseCourseTests
{
    [Test]
    public void AllLevelsAreValid()
    {
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            string name = "level " + level;
            Assert.Greater(p.Count, 50, name);
            Assert.Less(p.finishIndex, p.Count, name);
            Assert.Less(p.playerStart, p.finishIndex, name);
            Assert.Less(p.policeStart, p.playerStart, name + ": police must start behind the player");
            Assert.GreaterOrEqual(p.jumps, 1f, name + ": every level needs a jump");
            Assert.Greater(p.safePoints.Count, 3, name);
            Assert.Greater(p.length, 400f, name);
            Assert.Less(p.length, 2200f, name);
            Assert.IsTrue(p.solid[p.playerStart] && p.solid[p.finishIndex], name + ": start and finish on the road");
        }
    }

    [Test]
    public void CourseNeverTurnsBackOnItself()
    {
        // Směr se drží v ±85° od startu, takže z stále roste a dráha se nemůže zkřížit
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            for (int i = 1; i < p.Count; i++)
                Assert.Greater(p.points[i].z, p.points[i - 1].z, "level " + level + " goes backwards at " + i);
        }
    }

    [Test]
    public void GapsAreJumpable()
    {
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            int gap = 0;
            for (int i = 0; i < p.Count; i++)
            {
                if (!p.solid[i]) { gap++; continue; }
                Assert.LessOrEqual(gap * ObstacleCourse.Step, 27f, "level " + level + ": gap too long");
                if (gap > 0)
                {
                    // Před každou mezerou je skokanka
                    Assert.AreEqual(ObstacleCourse.Feature.Ramp, p.features[i - gap - 1], "level " + level);
                }
                gap = 0;
            }
        }
    }

    [Test]
    public void SafePointsAreOnSolidRoad()
    {
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            foreach (int s in p.safePoints)
            {
                Assert.IsTrue(p.solid[s], "level " + level);
                Assert.AreNotEqual(ObstacleCourse.Feature.Gap, p.features[s]);
            }
            Assert.LessOrEqual(p.SafeBefore(p.finishIndex), p.finishIndex);
        }
    }

    [Test]
    public void ObstaclesLeaveRoomToPass()
    {
        for (int level = 1; level <= ChaseLevels.Count; level++)
        {
            ObstacleCourse.Plan p = ObstacleCourse.Generate(level);
            foreach (ObstacleCourse.Item item in p.items)
            {
                int i = p.Nearest(item.position, 0, 0, p.Count);
                float lateral = Mathf.Abs(Vector3.Dot(item.position - p.points[i], p.Right(i)));
                Assert.Less(lateral, p.widths[i] * 0.5f, "level " + level + ": " + item.type + " off the road");
            }
        }
    }

    [Test]
    public void GenerationIsDeterministic()
    {
        ObstacleCourse.Plan a = ObstacleCourse.Generate(7), b = ObstacleCourse.Generate(7);
        Assert.AreEqual(a.Count, b.Count);
        Assert.AreEqual(a.items.Count, b.items.Count);
        Assert.AreEqual(a.length, b.length, 0.001f);
    }

    [Test]
    public void StarsGetHarderWithTime()
    {
        Assert.AreEqual(3, ChaseLevels.StarsFor(30f, 1000f));
        Assert.AreEqual(2, ChaseLevels.StarsFor(45f, 1000f));
        Assert.AreEqual(1, ChaseLevels.StarsFor(90f, 1000f));
        Assert.Greater(ChaseLevels.PoliceSkill(15), ChaseLevels.PoliceSkill(1));
        Assert.GreaterOrEqual(ChaseLevels.PoliceCount(15), ChaseLevels.PoliceCount(1));
    }
}
