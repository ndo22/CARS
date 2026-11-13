namespace UnityEngine.UI {
  public class Graphic : UnityEngine.MonoBehaviour { public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.Canvas canvas => null; public UnityEngine.RectTransform rectTransform => null; }
  public class Text : Graphic { public string text; public UnityEngine.Font font; public int fontSize; public UnityEngine.FontStyle fontStyle; public UnityEngine.TextAnchor alignment; public UnityEngine.HorizontalWrapMode horizontalOverflow; public UnityEngine.VerticalWrapMode verticalOverflow; }
  public class Image : Graphic {}
  public class RawImage : Graphic { public UnityEngine.Texture texture; }
  public class Shadow : UnityEngine.MonoBehaviour { public UnityEngine.Color effectColor; public UnityEngine.Vector2 effectDistance; }
  public class Outline : Shadow {}
  public class Slider : UnityEngine.MonoBehaviour { public float value; public void SetValueWithoutNotify(float v){} }
  public struct ColorBlock { public UnityEngine.Color highlightedColor, pressedColor; }
  public class Button : UnityEngine.MonoBehaviour { public class ButtonClickedEvent : UnityEngine.Events.UnityEvent {} public ButtonClickedEvent onClick; public ColorBlock colors; public Graphic targetGraphic; }
  public class CanvasScaler : UnityEngine.MonoBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize } public ScaleMode uiScaleMode; public UnityEngine.Vector2 referenceResolution; public float matchWidthOrHeight; }
  public class GraphicRaycaster : UnityEngine.MonoBehaviour {}
}
namespace UnityEngine.EventSystems { public class EventSystem : UnityEngine.MonoBehaviour {} public class StandaloneInputModule : UnityEngine.MonoBehaviour {} }
namespace TMPro {
  public enum TextAlignmentOptions { Left, Right, Center }
  public class TMP_FontAsset : UnityEngine.ScriptableObject {}
  public class TextMeshProUGUI : UnityEngine.UI.Graphic { public string text; public TMP_FontAsset font; public float fontSize; public TextAlignmentOptions alignment; }
}
namespace UnityEngine.EventSystems {
  public enum EventTriggerType { PointerDown, PointerUp, PointerExit }
  public class BaseEventData {}
  public class EventTrigger : UnityEngine.MonoBehaviour {
    public class TriggerEvent : UnityEngine.Events.UnityEvent<BaseEventData> {}
    public class Entry { public EventTriggerType eventID; public TriggerEvent callback = new TriggerEvent(); }
    public System.Collections.Generic.List<Entry> triggers = new System.Collections.Generic.List<Entry>();
  }
}
