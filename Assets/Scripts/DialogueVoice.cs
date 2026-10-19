using TMPro;
using UnityEngine;

/// <summary>One voice at a time: short syllables follow revealed letters, cycling through the first second of the sample.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1500)]
public sealed class DialogueVoice : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip robotVoice, humanVoice;
    [SerializeField] private Transform robotSpeaker, operatorSpeaker;
    private TMP_Text text;
    private bool human, simulateTyping;
    private int lastVisible;
    private float began, sampleCursor, nextSyllable;
    private Transform activeSpeaker;
    public int SyllablesPlayed { get; private set; }

    public void Begin(TMP_Text dialogue, bool operatorC, bool alreadyVisible = false, Transform speakerOverride = null)
    {
        End(); text = dialogue; human = operatorC; simulateTyping = alreadyVisible;
        began = Time.unscaledTime; lastVisible = 0; sampleCursor = 0; nextSyllable = 0;
        activeSpeaker=speakerOverride;SyllablesPlayed=0;
        var clip = human ? humanVoice : robotVoice;
        if (clip && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        if (source) { source.ignoreListenerVolume = false; source.spatialBlend = 1; source.dopplerLevel = 0; source.loop = false; }
    }
    private void LateUpdate()
    {
        if (!text || !text.isActiveAndEnabled || !source) return;
        Transform speaker = activeSpeaker ? activeSpeaker : human ? operatorSpeaker : robotSpeaker;
        if (speaker) source.transform.position = speaker.TryGetComponent<Renderer>(out var renderer) ? renderer.bounds.center : speaker.position;
        AudioClip clip = human ? humanVoice : robotVoice;
        if (!clip || clip.loadState != AudioDataLoadState.Loaded) return;
        var info = text.textInfo;
        int visible = Mathf.Min(info.characterCount, simulateTyping ? (int)((Time.unscaledTime - began) / .065f) : text.maxVisibleCharacters);
        bool syllable = false;
        for (int i = lastVisible; i < visible; i++) if (char.IsLetterOrDigit(info.characterInfo[i].character)) syllable = true;
        lastVisible = visible;
        if (!syllable || Time.unscaledTime < nextSyllable) return;
        if (!clip || clip.length <= .001f) return;
        float window = Mathf.Min(1f, clip.length);
        float duration = Mathf.Min(.065f, window);
        if (sampleCursor + duration > window) sampleCursor = 0;
        source.Stop(); source.clip = clip; source.timeSamples = Mathf.FloorToInt(sampleCursor*clip.frequency); source.Play();
        source.SetScheduledEndTime(AudioSettings.dspTime + duration);
        sampleCursor += duration; nextSyllable = Time.unscaledTime + .045f;
        SyllablesPlayed++;
    }
    public void End() { if (source) source.Stop(); text = null; }
    private void OnDisable() => End();
}
