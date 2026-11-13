using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Tlačítka na obrazovce pro mobil: zatáčení vlevo dole, plyn/brzda vpravo dole, nitro, drift, reset, pauza.</summary>
public class TouchControls : MonoBehaviour
{
    public static TouchControls Create(Transform canvas)
    {
        TouchInput.Enabled = true;
        TouchInput.Tilt = GameSettings.Tilt;
        var go = new GameObject("TouchControls", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        LowPolyUI.Stretch((RectTransform)go.transform);
        var tc = go.AddComponent<TouchControls>();
        tc.Build((RectTransform)go.transform);
        return tc;
    }

    private void Build(RectTransform root)
    {
        if (!GameSettings.Tilt)
        {
            Pad(root, "<", GameInput.Action.Left, new Vector2(0f, 0f), new Vector2(40f, 40f), 170f);
            Pad(root, ">", GameInput.Action.Right, new Vector2(0f, 0f), new Vector2(230f, 40f), 170f);
        }
        Pad(root, "GAS", GameInput.Action.Accelerate, new Vector2(1f, 0f), new Vector2(-40f, 40f), 190f);
        Pad(root, "BRAKE", GameInput.Action.Brake, new Vector2(1f, 0f), new Vector2(-250f, 40f), 150f);
        Pad(root, "NITRO", GameInput.Action.Nitro, new Vector2(1f, 0f), new Vector2(-40f, 250f), 130f);
        Pad(root, "DRIFT", GameInput.Action.Handbrake, new Vector2(1f, 0f), new Vector2(-250f, 210f), 130f);
        Pad(root, "R", GameInput.Action.Reset, new Vector2(1f, 1f), new Vector2(-180f, -120f), 80f);
        Pad(root, "II", GameInput.Action.Pause, new Vector2(1f, 1f), new Vector2(-80f, -120f), 80f);
    }

    private static void Pad(RectTransform root, string label, GameInput.Action action, Vector2 anchor, Vector2 position, float size)
    {
        RectTransform rect = LowPolyUI.Panel(root, label + "Pad", new Color(1f, 1f, 1f, 0.18f));
        LowPolyUI.Place(rect, anchor, position, new Vector2(size, size));
        rect.GetComponent<Image>().raycastTarget = true;
        Text t = LowPolyUI.Label(rect, label, Mathf.RoundToInt(size * 0.22f), TextAnchor.MiddleCenter, Color.white, "Label");
        LowPolyUI.Stretch(t.rectTransform);

        var trigger = rect.gameObject.AddComponent<EventTrigger>();
        Add(trigger, EventTriggerType.PointerDown, () => TouchInput.Set(action, true));
        Add(trigger, EventTriggerType.PointerUp, () => TouchInput.Set(action, false));
        Add(trigger, EventTriggerType.PointerExit, () => TouchInput.Set(action, false));
    }

    private static void Add(EventTrigger trigger, EventTriggerType type, System.Action action)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => action());
        trigger.triggers.Add(entry);
    }

    private void OnDestroy()
    {
        TouchInput.ReleaseAll();
        TouchInput.Enabled = false;
    }
}
