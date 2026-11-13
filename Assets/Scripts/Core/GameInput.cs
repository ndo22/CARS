using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jednotné ovládání: klávesnice (přemapovatelná, uložená v PlayerPrefs), gamepad a dotykové ovládání.
/// Gamepad: levá páčka zatáčí, RT plyn, LT brzda, A nitro, X ruční brzda, Y kamera, Back reset, Start pauza.
///
/// Hráč: -1 = jeden hráč (všechna zařízení), 0 = hráč 1 ve split-screenu (přemapovatelné klávesy + gamepad 1),
/// 1 = hráč 2 (šipky, pravý Ctrl drift, pravý Shift nitro, Enter reset + gamepad 2).
/// </summary>
public static class GameInput
{
    public enum Action { Accelerate, Brake, Left, Right, Handbrake, Nitro, Reset, Camera, Pause }

    public static readonly Action[] AllActions = (Action[])Enum.GetValues(typeof(Action));

    private static readonly KeyCode[] Defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift, KeyCode.R, KeyCode.C, KeyCode.Escape
    };

    // Pevné záložní klávesy pro jednoho hráče (šipky apod.)
    private static readonly KeyCode[] Alternates =
    {
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.None, KeyCode.RightShift, KeyCode.None, KeyCode.None, KeyCode.P
    };

    // Klávesy hráče 2 ve split-screenu
    private static readonly KeyCode[] PlayerTwo =
    {
        KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.RightControl, KeyCode.RightShift,
        KeyCode.Return, KeyCode.None, KeyCode.P
    };

    // Tlačítka gamepadu: index tlačítka (JoystickButtonN)
    private static readonly int[] PadButtons = { -1, -1, -1, -1, 2, 0, 6, 3, 7 };

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

    // ---------------------------------------------------------------- tlačítka

    public static bool Held(Action action, int player = -1) => Check(action, player, Input.GetKey) || TouchInput.Held(action, player);

    public static bool Pressed(Action action, int player = -1) => Check(action, player, Input.GetKeyDown) || TouchInput.Pressed(action, player);

    private static bool Check(Action action, int player, Func<KeyCode, bool> test)
    {
        int i = (int)action;
        if (player == 1)
        {
            if (PlayerTwo[i] != KeyCode.None && test(PlayerTwo[i])) return true;
        }
        else
        {
            if (test(Bindings[i])) return true;
            if (player < 0 && Alternates[i] != KeyCode.None && test(Alternates[i])) return true;
        }
        if (PadButtons[i] >= 0)
        {
            // -1: libovolný gamepad, 0: gamepad 1, 1: gamepad 2
            KeyCode pad = player < 0 ? KeyCode.JoystickButton0 : (player == 0 ? KeyCode.Joystick1Button0 : KeyCode.Joystick2Button0);
            if (test(pad + PadButtons[i])) return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- osy

    /// <summary>0..1 plyn (klávesa, pravý trigger, páčka nahoru, dotyk).</summary>
    public static float Throttle(int player = -1)
    {
        float key = Held(Action.Accelerate, player) ? 1f : 0f;
        return Mathf.Max(key, Mathf.Max(Axis("PadRT", player), Mathf.Max(0f, Axis("PadStickY", player))));
    }

    /// <summary>0..1 brzda / couvání (klávesa, levý trigger, páčka dolů, dotyk).</summary>
    public static float Brake(int player = -1)
    {
        float key = Held(Action.Brake, player) ? 1f : 0f;
        return Mathf.Max(key, Mathf.Max(Axis("PadLT", player), Mathf.Max(0f, -Axis("PadStickY", player))));
    }

    /// <summary>-1..1 zatáčení bez vyhlazení (vyhlazuje auto samo).</summary>
    public static float Steer(int player = -1)
    {
        float key = (Held(Action.Right, player) ? 1f : 0f) - (Held(Action.Left, player) ? 1f : 0f);
        float pad = Axis("PadSteer", player);
        float touch = TouchInput.Steer(player);
        float best = key;
        if (Mathf.Abs(pad) > Mathf.Abs(best)) best = pad;
        if (Mathf.Abs(touch) > Mathf.Abs(best)) best = touch;
        return Mathf.Clamp(best, -1f, 1f);
    }

    // Osa, která nemusí v InputManageru existovat (starší kopie projektu) - pak vrací 0.
    // Hráči ve split-screenu mají vlastní osy s číslem gamepadu (PadSteer1, PadSteer2...).
    private static readonly Dictionary<string, bool> axisExists = new Dictionary<string, bool>();

    private static float Axis(string name, int player)
    {
        if (player >= 0) name += (player + 1);
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

/// <summary>Stav dotykových tlačítek na obrazovce (nastavuje <see cref="TouchControls"/>).</summary>
public static class TouchInput
{
    private static readonly bool[] held = new bool[Enum.GetValues(typeof(GameInput.Action)).Length];
    private static readonly int[] pressedFrame = new int[held.Length];
    public static bool Enabled;
    public static bool Tilt;

    public static void Set(GameInput.Action action, bool down)
    {
        int i = (int)action;
        if (down && !held[i]) pressedFrame[i] = Time.frameCount;
        held[i] = down;
    }

    public static bool Held(GameInput.Action action, int player) => Enabled && player <= 0 && held[(int)action];

    public static bool Pressed(GameInput.Action action, int player) =>
        Enabled && player <= 0 && pressedFrame[(int)action] == Time.frameCount && held[(int)action];

    /// <summary>Náklon telefonu jako volant (když je zapnutý).</summary>
    public static float Steer(int player)
    {
        if (!Enabled || !Tilt || player > 0) return 0f;
        return Mathf.Clamp(Input.acceleration.x * 2.2f, -1f, 1f);
    }

    public static void ReleaseAll()
    {
        for (int i = 0; i < held.Length; i++) held[i] = false;
    }
}
