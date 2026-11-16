using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Minimapa v rohu obrazovky: obrys tratě (vykreslený do textury) a tečky závodníků.</summary>
public class Minimap : MonoBehaviour
{
    private const int TextureSize = 256;

    private RectTransform area;
    private Vector2 worldMin;
    private float worldScale;
    private readonly List<KeyValuePair<Transform, RectTransform>> dots = new List<KeyValuePair<Transform, RectTransform>>();

    public static Minimap Create(RectTransform parent, Vector3[] track, float roadWidth, float size)
    {
        var go = new GameObject("Minimap", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(24f, 24f);
        rect.sizeDelta = new Vector2(size, size);

        var background = go.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.35f);
        background.raycastTarget = false;

        var map = go.AddComponent<Minimap>();
        map.Build(track, roadWidth, rect);
        return map;
    }

    private void Build(Vector3[] track, float roadWidth, RectTransform rect)
    {
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var p in track)
        {
            min = Vector2.Min(min, new Vector2(p.x, p.z));
            max = Vector2.Max(max, new Vector2(p.x, p.z));
        }
        float extent = Mathf.Max(max.x - min.x, max.y - min.y) * 1.12f;
        Vector2 center = (min + max) * 0.5f;
        worldMin = center - Vector2.one * extent * 0.5f;
        worldScale = 1f / extent;

        // Textura s tratí: tmavý obrys + světlá silnice + startovní čára
        var pixels = new Color32[TextureSize * TextureSize];
        float roadPx = Mathf.Max(2.5f, roadWidth * worldScale * TextureSize * 0.5f);
        Stamp(pixels, track, roadPx + 2.2f, new Color32(20, 20, 25, 230));
        Stamp(pixels, track, roadPx, new Color32(235, 235, 235, 255));
        Vector2 start = ToUv(track[0]) * TextureSize;
        Disc(pixels, start, roadPx + 1.5f, new Color32(255, 70, 60, 255));

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply();

        var imageGo = new GameObject("Track", typeof(RectTransform));
        imageGo.transform.SetParent(rect, false);
        area = (RectTransform)imageGo.transform;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = Vector2.one * 8f;
        area.offsetMax = -Vector2.one * 8f;
        var raw = imageGo.AddComponent<RawImage>();
        raw.texture = texture;
        raw.raycastTarget = false;
    }

    public void AddRacer(Transform racer, Color color, bool isPlayer)
    {
        var go = new GameObject("Dot", typeof(RectTransform));
        go.transform.SetParent(area, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        float size = isPlayer ? 16f : 11f;
        rect.sizeDelta = new Vector2(size, size);
        rect.localRotation = Quaternion.Euler(0f, 0f, 45f); // kosočtverec
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = isPlayer ? Color.white : new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, 2f);
        if (isPlayer) rect.SetAsLastSibling();
        dots.Add(new KeyValuePair<Transform, RectTransform>(racer, rect));
    }

    private void LateUpdate()
    {
        if (area == null) return;
        Vector2 size = area.rect.size;
        foreach (var pair in dots)
        {
            if (pair.Key == null) continue;
            // Vyřazené auto (vypnutý objekt) na mapě nezobrazovat
            bool visible = pair.Key.gameObject.activeInHierarchy;
            if (pair.Value.gameObject.activeSelf != visible) pair.Value.gameObject.SetActive(visible);
            if (!visible) continue;
            Vector2 uv = ToUv(pair.Key.position);
            pair.Value.anchoredPosition = new Vector2((uv.x - 0.5f) * size.x, (uv.y - 0.5f) * size.y);
        }
    }

    private Vector2 ToUv(Vector3 world)
    {
        return new Vector2((world.x - worldMin.x) * worldScale, (world.z - worldMin.y) * worldScale);
    }

    private void Stamp(Color32[] pixels, Vector3[] track, float radius, Color32 color)
    {
        for (int i = 0; i < track.Length; i++)
        {
            Vector2 a = ToUv(track[i]) * TextureSize;
            Vector2 b = ToUv(track[(i + 1) % track.Length]) * TextureSize;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int s = 0; s < steps; s++)
                Disc(pixels, Vector2.Lerp(a, b, s / (float)steps), radius, color);
        }
    }

    private static void Disc(Color32[] pixels, Vector2 c, float r, Color32 color)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r)), x1 = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(c.x + r));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r)), y1 = Mathf.Min(TextureSize - 1, Mathf.CeilToInt(c.y + r));
        float r2 = r * r;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dx = x + 0.5f - c.x, dy = y + 0.5f - c.y;
                if (dx * dx + dy * dy <= r2)
                    pixels[y * TextureSize + x] = color;
            }
    }
}
