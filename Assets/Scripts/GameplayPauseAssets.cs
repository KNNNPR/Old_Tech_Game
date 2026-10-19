using TMPro;
using UnityEngine;

/// <summary>Shared font and volume-controlled feedback for the in-game settings window.</summary>
public sealed class GameplayPauseAssets:ScriptableObject
{
    public TMP_FontAsset font;
    public AudioClip hover,press,back;
}
