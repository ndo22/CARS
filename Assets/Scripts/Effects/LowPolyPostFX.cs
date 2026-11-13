using UnityEngine;

/// <summary>
/// Vlastní lehký postprocessing pro built-in render pipeline (bez balíčku):
/// bloom, sytost, kontrast, tónování a vinětace. Vypíná se v nastavení (GameSettings.PostFx).
/// </summary>
[RequireComponent(typeof(Camera))]
public class LowPolyPostFX : MonoBehaviour
{
    public Shader shader;
    [Range(0f, 2f)] public float threshold = 0.85f;
    [Range(0f, 3f)] public float bloomIntensity = 0.7f;
    [Range(0f, 2f)] public float saturation = 1.15f;
    [Range(0.5f, 1.5f)] public float contrast = 1.06f;
    [Range(0f, 2f)] public float vignette = 0.9f;
    public float exposure = 1f;
    public Color tint = Color.white;
    [Range(1, 6)] public int iterations = 4;

    private Material material;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!GameSettings.PostFx || shader == null || !shader.isSupported)
        {
            Graphics.Blit(source, destination);
            return;
        }
        if (material == null)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        material.SetFloat("_Threshold", threshold);
        material.SetFloat("_BloomIntensity", bloomIntensity);
        material.SetFloat("_Saturation", saturation);
        material.SetFloat("_Contrast", contrast);
        material.SetFloat("_Vignette", vignette);
        material.SetFloat("_Exposure", exposure);
        material.SetColor("_Tint", tint);

        int w = source.width / 2, h = source.height / 2;
        var levels = new RenderTexture[iterations];
        RenderTexture current = RenderTexture.GetTemporary(w, h, 0, source.format);
        Graphics.Blit(source, current, material, 0);
        levels[0] = current;
        for (int i = 1; i < iterations; i++)
        {
            w = Mathf.Max(1, w / 2);
            h = Mathf.Max(1, h / 2);
            levels[i] = RenderTexture.GetTemporary(w, h, 0, source.format);
            Graphics.Blit(current, levels[i], material, 1);
            current = levels[i];
        }
        for (int i = iterations - 2; i >= 0; i--)
        {
            Graphics.Blit(current, levels[i], material, 2); // přičte se k nižší úrovni
            current = levels[i];
        }

        material.SetTexture("_BloomTex", current);
        Graphics.Blit(source, destination, material, 3);

        for (int i = 0; i < iterations; i++)
            RenderTexture.ReleaseTemporary(levels[i]);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
