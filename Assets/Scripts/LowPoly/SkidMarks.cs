using UnityEngine;

/// <summary>
/// Stopy smyku na silnici. Jeden sdílený mesh s kruhovým bufferem čtyřúhelníků,
/// auta do něj přidávají úseky přes <see cref="CarEffects"/>.
/// </summary>
public class SkidMarks : MonoBehaviour
{
    public static SkidMarks Instance { get; private set; }

    public int maxMarks = 1200;
    public float width = 0.3f;

    private Vector3[] vertices;
    private int next;
    private Mesh mesh;
    private bool dirty;

    public static SkidMarks Create(Material material)
    {
        var go = new GameObject("SkidMarks");
        var marks = go.AddComponent<SkidMarks>();
        marks.Setup(material);
        return marks;
    }

    private void Setup(Material material)
    {
        Instance = this;
        vertices = new Vector3[maxMarks * 4];
        var triangles = new int[maxMarks * 6];
        for (int i = 0; i < maxMarks; i++)
        {
            triangles[i * 6 + 0] = i * 4 + 0;
            triangles[i * 6 + 1] = i * 4 + 1;
            triangles[i * 6 + 2] = i * 4 + 2;
            triangles[i * 6 + 3] = i * 4 + 0;
            triangles[i * 6 + 4] = i * 4 + 2;
            triangles[i * 6 + 5] = i * 4 + 3;
        }
        var normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

        mesh = new Mesh { name = "SkidMarks" };
        mesh.MarkDynamic();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Přidá úsek stopy mezi dvěma body na zemi.</summary>
    public void AddSegment(Vector3 from, Vector3 to, Vector3 normal)
    {
        Vector3 dir = to - from;
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 side = Vector3.Cross(normal, dir.normalized) * (width * 0.5f);
        Vector3 lift = normal * 0.02f;

        int v = next * 4;
        // Pořadí vrcholů: levý-začátek, levý-konec, pravý-konec, pravý-začátek -> normála nahoru
        vertices[v + 0] = from - side + lift;
        vertices[v + 1] = to - side + lift;
        vertices[v + 2] = to + side + lift;
        vertices[v + 3] = from + side + lift;
        next = (next + 1) % maxMarks;
        dirty = true;
    }

    private void LateUpdate()
    {
        if (!dirty) return;
        mesh.vertices = vertices;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        dirty = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
