using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Jednoduchý skladač meshů pro low-poly styl. Každý trojúhelník má vlastní vrcholy,
/// takže po přepočtu normál je každá ploška rovná ("flat shading").
/// Submesh = jeden materiál (barva).
/// </summary>
public class MeshBuilder
{
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int>[] triangles;

    public MeshBuilder(int submeshCount = 1)
    {
        triangles = new List<int>[submeshCount];
        for (int i = 0; i < submeshCount; i++)
            triangles[i] = new List<int>();
    }

    public int VertexCount => vertices.Count;

    /// <summary>Trojúhelník; strana, kam míří normála, je dána pořadím (po směru hodinových ručiček = přední strana).</summary>
    public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        triangles[sub].Add(i);
        triangles[sub].Add(i + 1);
        triangles[sub].Add(i + 2);
    }

    /// <summary>Trojúhelník otočený tak, aby jeho normála mířila zhruba směrem <paramref name="facing"/>.</summary>
    public void TriFacing(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            Tri(sub, a, c, b);
        else
            Tri(sub, a, b, c);
    }

    /// <summary>Trojúhelník konvexního tělesa - normála míří od jeho středu.</summary>
    public void TriOutward(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 center)
    {
        TriFacing(sub, a, b, c, (a + b + c) / 3f - center);
    }

    public void QuadFacing(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
    {
        TriFacing(sub, a, b, c, facing);
        TriFacing(sub, a, c, d, facing);
    }

    public void QuadOutward(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 center)
    {
        TriOutward(sub, a, b, c, center);
        TriOutward(sub, a, c, d, center);
    }

    /// <summary>Kvádr (volitelně natočený) se středem <paramref name="center"/>.</summary>
    public void Box(int sub, Vector3 center, Vector3 size, Quaternion rotation)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            c[i] = center + rotation * local;
        }
        QuadOutward(sub, c[0], c[1], c[3], c[2], center); // -z
        QuadOutward(sub, c[4], c[5], c[7], c[6], center); // +z
        QuadOutward(sub, c[0], c[1], c[5], c[4], center); // -y
        QuadOutward(sub, c[2], c[3], c[7], c[6], center); // +y
        QuadOutward(sub, c[0], c[2], c[6], c[4], center); // -x
        QuadOutward(sub, c[1], c[3], c[7], c[5], center); // +x
    }

    public void Box(int sub, Vector3 center, Vector3 size)
    {
        Box(sub, center, size, Quaternion.identity);
    }

    /// <summary>Hranol / komolý kužel podél osy Y (strom, sloup). Při topRadius = 0 vznikne kužel.</summary>
    public void Cylinder(int sub, Vector3 baseCenter, float bottomRadius, float topRadius, float height, int sides, float angleOffset = 0f)
    {
        Vector3 top = baseCenter + Vector3.up * height;
        Vector3 center = baseCenter + Vector3.up * (height * 0.5f);
        for (int i = 0; i < sides; i++)
        {
            float a0 = angleOffset + i * Mathf.PI * 2f / sides;
            float a1 = angleOffset + (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            Vector3 d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 b0 = baseCenter + d0 * bottomRadius;
            Vector3 b1 = baseCenter + d1 * bottomRadius;

            if (topRadius > 0.001f)
            {
                Vector3 t0 = top + d0 * topRadius;
                Vector3 t1 = top + d1 * topRadius;
                QuadOutward(sub, b0, b1, t1, t0, center);
                TriFacing(sub, top, t0, t1, Vector3.up);
            }
            else
            {
                TriOutward(sub, b0, b1, top, center);
            }
            TriFacing(sub, baseCenter, b0, b1, Vector3.down);
        }
    }

    /// <summary>Kolo - válec s osou X. Běhoun do submeshe <paramref name="tireSub"/>, boky do <paramref name="hubSub"/>.</summary>
    public void Wheel(int tireSub, int hubSub, Vector3 center, float radius, float width, int sides)
    {
        float hw = width * 0.5f;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 d0 = new Vector3(0f, Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
            Vector3 d1 = new Vector3(0f, Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
            Vector3 l = center + Vector3.left * hw;
            Vector3 r = center + Vector3.right * hw;
            QuadOutward(tireSub, l + d0, l + d1, r + d1, r + d0, center);
            TriFacing(hubSub, l, l + d0, l + d1, Vector3.left);
            TriFacing(hubSub, r, r + d0, r + d1, Vector3.right);
            // hubSub == tireSub je běžné - boky pneumatiky tmavé, disk se přidá zvlášť
        }
    }

    /// <summary>Kamen - kvádr s náhodně posunutými rohy.</summary>
    public void Rock(int sub, Vector3 center, float size, System.Random rng)
    {
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -0.6f : 0.8f, (i & 4) == 0 ? -1f : 1f);
            Vector3 jitter = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 0.5f, (float)rng.NextDouble() - 0.5f) * 0.7f;
            c[i] = center + (Vector3.Scale(dir, new Vector3(0.5f, 0.5f, 0.5f)) + jitter * 0.5f) * size;
        }
        Vector3 mid = center + Vector3.up * (size * 0.1f);
        QuadOutward(sub, c[0], c[1], c[3], c[2], mid);
        QuadOutward(sub, c[4], c[5], c[7], c[6], mid);
        QuadOutward(sub, c[0], c[1], c[5], c[4], mid);
        QuadOutward(sub, c[2], c[3], c[7], c[6], mid);
        QuadOutward(sub, c[0], c[2], c[6], c[4], mid);
        QuadOutward(sub, c[1], c[3], c[7], c[5], mid);
    }

    /// <summary>Low-poly koule (UV sphere s málo segmenty).</summary>
    public void Ball(int sub, Vector3 center, float radius, int rings = 4, int segments = 7)
    {
        for (int r = 0; r < rings; r++)
        {
            float lat0 = Mathf.PI * r / rings - Mathf.PI * 0.5f;
            float lat1 = Mathf.PI * (r + 1) / rings - Mathf.PI * 0.5f;
            for (int s = 0; s < segments; s++)
            {
                float lon0 = Mathf.PI * 2f * s / segments;
                float lon1 = Mathf.PI * 2f * (s + 1) / segments;
                Vector3 a = center + Spherical(lat0, lon0) * radius;
                Vector3 b = center + Spherical(lat0, lon1) * radius;
                Vector3 c = center + Spherical(lat1, lon1) * radius;
                Vector3 d = center + Spherical(lat1, lon0) * radius;
                if (r == 0) TriOutward(sub, a, c, d, center);
                else if (r == rings - 1) TriOutward(sub, a, b, c, center);
                else QuadOutward(sub, a, b, c, d, center);
            }
        }
    }

    private static Vector3 Spherical(float lat, float lon)
    {
        return new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
    }

    /// <summary>Oboustranný trojúhelník (listy palem, vlajky) - viditelný z obou stran.</summary>
    public void TriDoubleSided(int sub, Vector3 a, Vector3 b, Vector3 c)
    {
        Tri(sub, a, b, c);
        Tri(sub, a, c, b);
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name };
        if (vertices.Count > 65000)
            mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.subMeshCount = triangles.Length;
        for (int i = 0; i < triangles.Length; i++)
            mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
