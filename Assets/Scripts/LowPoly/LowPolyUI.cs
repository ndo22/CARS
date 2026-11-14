using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pomocné funkce pro stavění UI kódem (panely, texty, tlačítka, skupiny voleb).</summary>
public static class LowPolyUI
{
    /// <summary>Písmo nadpisů (tučné Aquire - jen písmena, číslice, ! ? &lt; &gt;).</summary>
    public static Font font;
    /// <summary>Písmo pro čísla, časy a texty s interpunkcí (běžné Aquire má ' " . , -).</summary>
    public static Font numberFont;

    public static readonly Color Accent = new Color(1f, 0.78f, 0.18f);
    public static readonly Color ButtonColor = new Color(1f, 1f, 1f, 0.12f);
    /// <summary>Barva panelů - ve vysokém kontrastu téměř neprůhledná.</summary>
    public static Color PanelColor => new Color(0.03f, 0.05f, 0.1f, GameSettings.HighContrast ? 0.94f : 0.72f);

    /// <summary>Plátno vykreslené do výřezu kamery (split-screen - každý hráč má svůj HUD).</summary>
    public static Canvas CreateCanvas(string name, Camera camera)
    {
        Canvas canvas = CreateCanvas(name);
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1f;
        return canvas;
    }

    public static Canvas CreateCanvas(string name, int sortingOrder = 0)
    {
        if (UnityEngine.Object.FindObjectOfType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f) / GameSettings.UiScaleFactor;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static Sprite rounded;

    /// <summary>Bílý čtverec se zaoblenými rohy (9-slice) - vytvoří se jednou kódem, žádný soubor není potřeba.</summary>
    public static Sprite RoundedSprite
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 32, radius = 10;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Vzdálenost od nejbližšího středu rohového oblouku
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            rounded = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return rounded;
        }
    }

    public static RectTransform Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.sprite = RoundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = color.a > 0.3f;
        return (RectTransform)go.transform;
    }

    public static RectTransform Empty(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Text Label(Transform parent, string value, int size, TextAnchor alignment, Color color, string name = "Text", bool numeric = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<Text>();
        text.font = numeric && numberFont != null ? numberFont : font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.55f);
        return text;
    }

    /// <summary>Tlačítko s textem. Vrací pozadí (Image), aby šlo zvýraznit výběr.</summary>
    public static Image Button(Transform parent, string label, int fontSize, Action onClick, out Text text)
    {
        RectTransform rect = Panel(parent, label + "Button", ButtonColor);
        var image = rect.GetComponent<Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.92f, 0.7f, 1f);
        colors.pressedColor = new Color(1f, 0.7f, 0.3f, 1f);
        button.colors = colors;
        button.targetGraphic = image;
        rect.gameObject.AddComponent<UIHover>();
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -4f);
        button.onClick.AddListener(() =>
        {
            if (AudioManager.instance != null) AudioManager.instance.Play("Click");
            onClick?.Invoke();
        });

        text = Label(rect, label, fontSize, TextAnchor.MiddleCenter, Color.white, "Label");
        Stretch(text.rectTransform);
        return image;
    }

    public static Image Button(Transform parent, string label, int fontSize, Action onClick)
    {
        return Button(parent, label, fontSize, onClick, out _);
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void Stretch(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.one * padding;
        rect.offsetMax = -Vector2.one * padding;
    }

    /// <summary>Řada tlačítek, z nichž je vždy jedno vybrané (zvýrazněné).</summary>
    public class ChoiceGroup
    {
        private readonly List<Image> backgrounds = new List<Image>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Color> baseColors = new List<Color>();
        private readonly List<Outline> outlines = new List<Outline>();
        public int Selected { get; private set; } = -1;

        public void Add(Image background, Text label, Color baseColor)
        {
            backgrounds.Add(background);
            labels.Add(label);
            baseColors.Add(baseColor);
            var outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, 3f);
            outline.enabled = false;
            outlines.Add(outline);
        }

        public void Select(int index)
        {
            Selected = index;
            for (int i = 0; i < backgrounds.Count; i++)
            {
                bool on = i == index;
                bool swatch = labels[i] == null;
                backgrounds[i].color = swatch ? baseColors[i] : (on ? Accent : baseColors[i]);
                if (labels[i] != null) labels[i].color = on ? new Color(0.1f, 0.08f, 0.05f) : Color.white;
                outlines[i].enabled = on;
            }
        }
    }
}

/// <summary>Tlačítko se při najetí myší (nebo výběru gamepadem) mírně zvětší.</summary>
public class UIHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    public float scale = 1.05f;
    private bool over;

    public void OnPointerEnter(PointerEventData e) => over = true;
    public void OnPointerExit(PointerEventData e) => over = false;
    public void OnSelect(BaseEventData e) => over = true;
    public void OnDeselect(BaseEventData e) => over = false;

    private void OnDisable()
    {
        over = false;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        float target = over ? scale : 1f;
        float current = transform.localScale.x;
        if (Mathf.Abs(current - target) > 0.001f)
            transform.localScale = Vector3.one * Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 0.8f);
    }
}
