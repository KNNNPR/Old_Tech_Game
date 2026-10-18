using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameAction { Forward, Backward, Left, Right, Interact, Drop }

/// <summary>Shared by the menu and gameplay; preferences survive scene changes and restarts.</summary>
public static class GameSettings
{
    private const string Prefix = "PunchCard.Settings.";
    private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.E, Key.G };
    private static readonly Key[] Bindings = new Key[6];
    private static bool loaded;
    public static float Volume { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        loaded = false;
        Load();
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 0; i < Bindings.Length; i++)
        {
            var key = (Key)PlayerPrefs.GetInt(Prefix + (GameAction)i, (int)Defaults[i]);
            Bindings[i] = IsAllowed(key) ? key : Defaults[i];
        }
        Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "Volume", 1f));
        AudioListener.volume = Volume;
    }

    public static bool IsAllowed(Key key) => key != Key.None && key != Key.Escape && Enum.IsDefined(typeof(Key), key);

    public static Key Binding(GameAction action) { Load(); return Bindings[(int)action]; }
    public static bool IsPressed(GameAction action) => Keyboard.current != null && Keyboard.current[Binding(action)].isPressed;
    public static bool WasPressed(GameAction action) => Keyboard.current != null && Keyboard.current[Binding(action)].wasPressedThisFrame;

    public static string KeyLabel(GameAction action)
    {
        Key key = Binding(action);
        string label = Keyboard.current != null ? Keyboard.current[key].displayName : key.ToString();
        return string.IsNullOrEmpty(label) ? key.ToString().ToUpperInvariant() : label.ToUpperInvariant();
    }

    // Swap an occupied key so two actions never accidentally share the same binding.
    public static void Rebind(GameAction action, Key key)
    {
        Load();
        if (!IsAllowed(key)) return;
        int index = (int)action;
        Key previous = Bindings[index];
        for (int i = 0; i < Bindings.Length; i++)
            if (i != index && Bindings[i] == key) Bindings[i] = previous;
        Bindings[index] = key;
        SaveBindings();
    }

    public static void ResetBindings()
    {
        Load();
        Array.Copy(Defaults, Bindings, Defaults.Length);
        SaveBindings();
    }

    private static void SaveBindings()
    {
        for (int i = 0; i < Bindings.Length; i++) PlayerPrefs.SetInt(Prefix + (GameAction)i, (int)Bindings[i]);
        PlayerPrefs.Save();
    }

    public static void SetVolume(float volume)
    {
        Load();
        Volume = Mathf.Clamp01(volume);
        AudioListener.volume = Volume;
        PlayerPrefs.SetFloat(Prefix + "Volume", Volume);
        PlayerPrefs.Save();
    }

    public static string FormatPrompt(string prompt) => prompt.Replace("[E]", "[" + KeyLabel(GameAction.Interact) + "]")
        .Replace("[G]", "[" + KeyLabel(GameAction.Drop) + "]");
}
