using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Přehrává pojmenované zvuky (kliknutí, klakson...). Jedna instance přežívá mezi scénami.
/// AudioManager v další scéně se nesmaže (tlačítka na něj mají odkaz), jen předá své zvuky
/// hlavní instanci a volání Play/Stop přeposílá dál.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public Sound[] sounds;

    public static AudioManager instance;

    private readonly Dictionary<string, Sound> lookup = new Dictionary<string, Sound>();

    void Awake()
    {
        if (instance != null && instance != this)
        {
            // Zaregistrovat zvuky, které hlavní instance ještě nezná (např. zvuky motoru ze závodní scény)
            if (sounds != null)
            {
                foreach (Sound s in sounds)
                    instance.Register(s);
            }
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (sounds != null)
        {
            foreach (Sound s in sounds)
                Register(s);
        }
    }

    private void Register(Sound s)
    {
        if (s == null || string.IsNullOrEmpty(s.name) || lookup.ContainsKey(s.name))
            return;

        s.source = gameObject.AddComponent<AudioSource>();
        s.source.clip = s.clip;
        s.source.volume = s.volume;
        s.source.pitch = s.pitch;
        s.source.loop = s.loop;
        s.source.playOnAwake = false;
        // Zvuky menu musí hrát i při pauze
        s.source.ignoreListenerPause = true;

        lookup.Add(s.name, s);
    }

    private Sound Find(string name)
    {
        AudioManager target = instance != null ? instance : this;
        if (target.lookup.TryGetValue(name, out Sound s))
            return s;

        Debug.LogWarning("Sound: " + name + " not found!");
        return null;
    }

    public void Play(string name)
    {
        Find(name)?.source.Play();
    }

    public void Stop(string name)
    {
        Find(name)?.source.Stop();
    }

    public bool IsPlaying(string name)
    {
        Sound s = Find(name);
        return s != null && s.source.isPlaying;
    }
}
