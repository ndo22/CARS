using System;
using UnityEngine;

/// <summary>
/// Jednotné ovládání: klávesnice (přemapovatelná, uložená v PlayerPrefs) + gamepad.
/// Gamepad: levá páčka zatáčí, RT plyn, LT brzda, A nitro, X ruční brzda, Y kamera, Back reset, Start pauza.
/// </summary>
public static class GameInput
{
    public enum Action { Accelerate, Brake, Left, Right, Handbrake, Nitro, Reset, Camera, Pause }

    public static readonly Action[] AllActions = (Action[])Enum.GetValues(typeof(Action));

    private static readonly KeyCode[] Defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.R, KeyCode.C, KeyCode.Escape
    };

    // Pevné záložní klávesy (šipky apod.), které platí vždy
    private static readonly KeyCode[] Alternates =
    {
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.None, KeyCode.RightShift, KeyCode.None, KeyCode.None, KeyCode.P
    };

    private static readonly KeyCode[] PadButtons =
    {
        KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.None, KeyCode.JoystickButton2, KeyCode.JoystickButton0,
        KeyCode.JoystickButton6, KeyCode.JoystickButton3, KeyCode.JoystickButton7
    };

    private static KeyCode[] bindings;

    private static KeyCode[] Bindings
    {
        get
        {
            if (bindings == null)
            {
                bindings = new KeyCode[Defaults.Length];
                for (int i = 0; i < Defaults.Length; i++)
                    bindings[i] = (KeyCode)PlayerPrefs.GetInt("key_" + (Action)i, (int)Defaults[i]);
            }
            return bindings;
        }
    }

    public static KeyCode GetKey(Action action) => Bindings[(int)action];

    public static void Rebind(Action action, KeyCode key)
    {
        Bindings[(int)action] = key;
        PlayerPrefs.SetInt("key_" + action, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetBindings()
    {
        for (int i = 0; i < Defaults.Length; i++)
            Rebind((Action)i, Defaults[i]);
    }

    public static string PadLabel(Action action)
    {
        switch (action)
        {
            case Action.Accelerate: return "RT";
            case Action.Brake: return "LT";
            case Action.Left:
            case Action.Right: return "STICK";
            case Action.Handbrake: return "X";
            case Action.Nitro: return "A";
            case Action.Reset: return "BACK";
            case Action.Camera: return "Y";
            default: return "START";
        }
    }

    public static bool Held(Action action)
    {
        int i = (int)action;
        return Input.GetKey(Bindings[i]) || (Alternates[i] != KeyCode.None && Input.GetKey(Alternates[i]))
            || (PadButtons[i] != KeyCode.None && Input.GetKey(PadButtons[i]));
    }

    public static bool Pressed(Action action)
    {
        int i = (int)action;
        return Input.GetKeyDown(Bindings[i]) || (Alternates[i] != KeyCode.None && Input.GetKeyDown(Alternates[i]))
            || (PadButtons[i] != KeyCode.None && Input.GetKeyDown(PadButtons[i]));
    }

    /// <summary>0..1 plyn (klávesa, pravý trigger, páčka nahoru).</summary>
    public static float Throttle()
    {
        float key = Held(Action.Accelerate) ? 1f : 0f;
        return Mathf.Max(key, Mathf.Max(Axis("PadRT"), Mathf.Max(0f, Axis("PadStickY"))));
    }

    /// <summary>0..1 brzda / couvání (klávesa, levý trigger, páčka dolů).</summary>
    public static float Brake()
    {
        float key = Held(Action.Brake) ? 1f : 0f;
        return Mathf.Max(key, Mathf.Max(Axis("PadLT"), Mathf.Max(0f, -Axis("PadStickY"))));
    }

    /// <summary>-1..1 zatáčení bez vyhlazení (vyhlazuje auto samo).</summary>
    public static float Steer()
    {
        float key = (Held(Action.Right) ? 1f : 0f) - (Held(Action.Left) ? 1f : 0f);
        float pad = Axis("PadSteer");
        return Mathf.Clamp(Mathf.Abs(pad) > Mathf.Abs(key) ? pad : key, -1f, 1f);
    }

    // Osa, která nemusí v InputManageru existovat (starší kopie projektu) - pak vrací 0
    private static readonly System.Collections.Generic.Dictionary<string, bool> axisExists = new System.Collections.Generic.Dictionary<string, bool>();

    private static float Axis(string name)
    {
        if (axisExists.TryGetValue(name, out bool exists) && !exists) return 0f;
        try
        {
            float v = Input.GetAxis(name);
            axisExists[name] = true;
            return v;
        }
        catch (ArgumentException)
        {
            axisExists[name] = false;
            return 0f;
        }
    }
}
