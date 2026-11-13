using UnityEngine;

/// <summary>Seznam všech low-poly tratí. Novou trať přidáš dalším záznamem v poli <see cref="All"/>.</summary>
public static class TrackLibrary
{
    public class TrackDefinition
    {
        public string id;
        public string name;
        public string description;
        public System.Func<TrackTheme> theme;
        public Vector3[] points;
        public float heightScale = 2f;
        public float roadWidth = 14f;
        public int seed = 1234;

        public string LeaderboardKey => "leaderboard_" + id;
    }

    public static readonly TrackDefinition[] All =
    {
        new TrackDefinition
        {
            id = "valley", name = "GREEN VALLEY", description = "Flowing hills, windmills and pine forests",
            theme = TrackTheme.Meadow, seed = 1234, heightScale = 2f,
            points = TrackGenerator.DefaultControlPoints(),
        },
        new TrackDefinition
        {
            id = "canyon", name = "DESERT CANYON", description = "Long and fast between red mesas",
            theme = TrackTheme.Desert, seed = 77, heightScale = 2f, roadWidth = 15f,
            points = new[]
            {
                new Vector3(0, 0, -150), new Vector3(0, 0, 150), new Vector3(30, 1, 230), new Vector3(110, 2, 260),
                new Vector3(200, 3, 230), new Vector3(240, 4, 160), new Vector3(300, 4, 120), new Vector3(400, 3, 140),
                new Vector3(480, 2, 110), new Vector3(500, 2, 20), new Vector3(460, 1, -60), new Vector3(380, 1, -80),
                new Vector3(320, 1, -150), new Vector3(330, 1, -240), new Vector3(280, 1, -310), new Vector3(180, 1, -320),
                new Vector3(90, 0, -290), new Vector3(20, 0, -230),
            },
        },
        new TrackDefinition
        {
            id = "alpine", name = "ALPINE PASS", description = "Tight, technical and 20 m of climbing",
            theme = TrackTheme.Snow, seed = 4242, heightScale = 2.5f, roadWidth = 13f,
            points = new[]
            {
                new Vector3(0, 0, -60), new Vector3(0, 0, 50), new Vector3(15, 1, 95), new Vector3(60, 2, 110),
                new Vector3(90, 3, 80), new Vector3(85, 4, 30), new Vector3(110, 5, -5), new Vector3(160, 6, 0),
                new Vector3(190, 7, 40), new Vector3(180, 8, 95), new Vector3(200, 8, 140), new Vector3(250, 7, 150),
                new Vector3(290, 6, 115), new Vector3(280, 5, 60), new Vector3(300, 4, 10), new Vector3(290, 3, -50),
                new Vector3(240, 2, -80), new Vector3(190, 2, -60), new Vector3(140, 1, -90), new Vector3(100, 1, -140),
                new Vector3(50, 0, -140), new Vector3(10, 0, -110),
            },
        },
        new TrackDefinition
        {
            id = "coast", name = "SUNSET COAST", description = "Palms, beaches and a lighthouse by the sea",
            theme = TrackTheme.Coast, seed = 909, heightScale = 2f,
            points = new[]
            {
                new Vector3(0, 0, -100), new Vector3(0, 0, 80), new Vector3(20, 0, 140), new Vector3(70, 1, 170),
                new Vector3(130, 1, 160), new Vector3(170, 1, 120), new Vector3(220, 1, 130), new Vector3(260, 1, 180),
                new Vector3(320, 1, 190), new Vector3(360, 1, 140), new Vector3(350, 1, 60), new Vector3(320, 1, 0),
                new Vector3(340, 1, -70), new Vector3(310, 1, -140), new Vector3(240, 1, -160), new Vector3(180, 1, -120),
                new Vector3(120, 1, -150), new Vector3(60, 1, -170), new Vector3(15, 0, -140),
            },
        },
    };

    public static TrackGenerator.TrackSettings Settings(TrackDefinition d)
    {
        TrackTheme theme = d.theme();
        return new TrackGenerator.TrackSettings
        {
            controlPoints = d.points,
            heightScale = d.heightScale,
            roadWidth = d.roadWidth,
            seed = d.seed,
            treeCount = theme.treeCount,
            rockCount = theme.rockCount,
            cloudCount = theme.cloudCount,
        };
    }
}
