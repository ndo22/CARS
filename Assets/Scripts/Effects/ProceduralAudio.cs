using UnityEngine;

/// <summary>
/// Zvuky a hudba generované kódem - nepotřebují žádné soubory ani licence.
/// </summary>
public static class ProceduralAudio
{
    private const int Rate = 44100;
    private static AudioClip squeal, nitro, impact, music, menuMusic;

    /// <summary>Pískání pneumatik (smyčka).</summary>
    public static AudioClip Squeal()
    {
        if (squeal != null) return squeal;
        int n = Rate; // 1 s
        var data = new float[n];
        var rng = new System.Random(3);
        float phase1 = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float wobble = Mathf.Sin(t * Mathf.PI * 2f * 7f) * 60f + Mathf.Sin(t * Mathf.PI * 2f * 3f) * 40f;
            phase1 += (1850f + wobble) / Rate;
            phase2 += (2630f - wobble * 0.7f) / Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.25f;
            data[i] = (Mathf.Sin(phase1 * Mathf.PI * 2f) * 0.45f + Mathf.Sin(phase2 * Mathf.PI * 2f) * 0.25f + lp * 0.3f) * 0.6f;
        }
        LoopFade(data, 400);
        squeal = Make("Squeal", data);
        return squeal;
    }

    /// <summary>Syčení nitra (smyčka).</summary>
    public static AudioClip Nitro()
    {
        if (nitro != null) return nitro;
        int n = Rate;
        var data = new float[n];
        var rng = new System.Random(5);
        float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.35f;
            hp = 0.97f * (hp + lp - prev);
            prev = lp;
            float t = i / (float)Rate;
            float rumble = Mathf.Sin(t * Mathf.PI * 2f * 55f) * 0.3f;
            data[i] = (hp * 0.7f + rumble) * 0.7f;
        }
        LoopFade(data, 600);
        nitro = Make("Nitro", data);
        return nitro;
    }

    /// <summary>Krátký náraz.</summary>
    public static AudioClip Impact()
    {
        if (impact != null) return impact;
        int n = Rate / 3;
        var data = new float[n];
        var rng = new System.Random(7);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Exp(-t * 18f);
            float noise = (float)rng.NextDouble() * 2f - 1f;
            lp += (noise - lp) * 0.2f;
            data[i] = (lp * 0.8f + Mathf.Sin(t * Mathf.PI * 2f * 70f) * 0.6f) * env;
        }
        impact = Make("Impact", data);
        return impact;
    }

    /// <summary>Závodní hudba - jednoduchý synthwave (basa, arpeggio, bicí), 8 taktů ve smyčce.</summary>
    public static AudioClip RaceMusic()
    {
        if (music == null) music = Song("RaceMusic", 128f, new[] { 0, 0, -4, -4, -7, -7, -2, -2 }, true);
        return music;
    }

    /// <summary>Klidnější hudba do menu.</summary>
    public static AudioClip MenuMusic()
    {
        if (menuMusic == null) menuMusic = Song("MenuMusic", 100f, new[] { 0, -5, -3, -7 }, false);
        return menuMusic;
    }

    private static AudioClip Song(string name, float bpm, int[] chordRoots, bool drums)
    {
        float beat = 60f / bpm;
        int bars = chordRoots.Length;
        int n = Mathf.RoundToInt(bars * 4 * beat * Rate);
        var data = new float[n];
        var rng = new System.Random(11);
        int[] arp = { 0, 7, 12, 16, 12, 7, 3, 7 };
        float baseFreq = 110f; // A2

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float beatPos = t / beat;
            int bar = Mathf.Min(bars - 1, (int)(beatPos / 4f));
            int root = chordRoots[bar];
            float s = 0f;

            // Basa - osminy
            float eighth = beatPos * 2f;
            float eighthT = (eighth - Mathf.Floor(eighth)) * beat * 0.5f;
            float bassF = baseFreq * 0.5f * Mathf.Pow(2f, root / 12f);
            float bassEnv = Mathf.Exp(-eighthT * 9f);
            s += Saw(t * bassF) * 0.22f * bassEnv;

            // Arpeggio - šestnáctiny
            float sixteenth = beatPos * 4f;
            int step = (int)sixteenth % arp.Length;
            float sixT = (sixteenth - Mathf.Floor(sixteenth)) * beat * 0.25f;
            float arpF = baseFreq * 2f * Mathf.Pow(2f, (root + arp[step]) / 12f);
            s += Square(t * arpF) * 0.07f * Mathf.Exp(-sixT * 14f);

            // Pad - dlouhé tóny
            float padF = baseFreq * Mathf.Pow(2f, (root + 12) / 12f);
            s += (Mathf.Sin(t * padF * Mathf.PI * 2f) + Mathf.Sin(t * padF * 1.5f * Mathf.PI * 2f) * 0.6f) * 0.05f;

            if (drums)
            {
                float beatT = (beatPos - Mathf.Floor(beatPos)) * beat;
                // Kopák na každou dobu
                s += Mathf.Sin(Mathf.PI * 2f * (50f * beatT + 400f * (1f - Mathf.Exp(-beatT * 30f)) / 30f)) * Mathf.Exp(-beatT * 12f) * 0.45f;
                // Virbl na 2 a 4
                int beatIndex = (int)beatPos % 4;
                if (beatIndex == 1 || beatIndex == 3)
                    s += ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-beatT * 22f) * 0.18f;
                // Hi-hat na osminy
                s += ((float)rng.NextDouble() * 2f - 1f) * Mathf.Exp(-eighthT * 60f) * 0.05f;
            }

            data[i] = Mathf.Clamp(s, -1f, 1f) * 0.8f;
        }
        LoopFade(data, 200);
        return Make(name, data);
    }

    private static float Saw(float x) => 2f * (x - Mathf.Floor(x + 0.5f));
    private static float Square(float x) => (x - Mathf.Floor(x)) < 0.5f ? 1f : -1f;

    private static void LoopFade(float[] data, int samples)
    {
        for (int i = 0; i < samples && i < data.Length; i++)
        {
            float k = i / (float)samples;
            data[i] *= k;
            data[data.Length - 1 - i] *= k;
        }
    }

    private static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
