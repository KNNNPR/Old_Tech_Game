using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-local room sound, card reading and player feedback. Master gain belongs to GameSettings.</summary>
[DisallowMultipleComponent]
public sealed class GameplaySceneAudio : MonoBehaviour
{
    [SerializeField] private AudioSource ambient, computerHum, reading, cameraMove, footsteps;
    [SerializeField, Range(0,1)] private float ambientVolume = .5f;
    [SerializeField] private AudioSource itemHandling, entranceDoorAudio, entranceDoorOpenAudio;
    [SerializeField] private AudioSource[] dropImpacts;
    [SerializeField, Min(.25f)] private float stepInterval = .62f;
    [SerializeField, Min(.05f)] private float stepDistance = .35f;
    [SerializeField] private FeedLever lever;
    [SerializeField] private FirstPersonController player;
    [SerializeField] private CharacterController character;
    private static readonly Dictionary<int, GameplaySceneAudio> scenes = new Dictionary<int, GameplaySceneAudio>();
    private Coroutine readingRoutine;
    private Vector3 lastPlayerPosition;
    private float walkDistance, readingVolume, nextStepTime;
    private float entranceOpenVolume, entranceCloseVolume;
    private bool scriptedWalking;
    private Transform scriptedWalker;
    private float[] impactVolumes;
    private int nextImpact;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetScenes() => scenes.Clear();
    public static GameplaySceneAudio For(Component owner) => owner && scenes.TryGetValue(owner.gameObject.scene.handle, out var audio) ? audio : null;
    private void OnEnable()
    {
        GameSettings.Load();
        scenes[gameObject.scene.handle] = this;
        foreach (var source in new[] { ambient, computerHum, reading, cameraMove, footsteps, itemHandling, entranceDoorAudio, entranceDoorOpenAudio })
            if (source) { source.ignoreListenerVolume = false; source.dopplerLevel = 0; }
        if (ambient && ambient.clip) { ambient.volume = ambientVolume; ambient.spatialBlend = 0; ambient.loop = true; ambient.Play(); }
        if (computerHum && computerHum.clip) { computerHum.loop = true; computerHum.Play(); }
        if (reading) readingVolume = reading.volume;
        if (entranceDoorOpenAudio) entranceOpenVolume = entranceDoorOpenAudio.volume;
        if (entranceDoorAudio) entranceCloseVolume = entranceDoorAudio.volume;
        if (player) lastPlayerPosition = player.transform.position;
        impactVolumes = new float[dropImpacts == null ? 0 : dropImpacts.Length];
        for (int i = 0; i < impactVolumes.Length; i++) if (dropImpacts[i])
        { impactVolumes[i] = dropImpacts[i].volume; dropImpacts[i].ignoreListenerVolume = false; }
        if (lever) { lever.FeedStarted += BeginReading; lever.CardEjected += OnEjected; }
    }
    public void PlayCameraMove()
    {
        if (!cameraMove || !cameraMove.clip) return;
        if (player) cameraMove.transform.position = player.transform.position + Vector3.up * 1.4f;
        cameraMove.Play();
    }
    public void PlayItemHandling() { if (itemHandling && itemHandling.clip) itemHandling.Play(); }
    public Coroutine PlayEntranceDoor(bool opening)
    {
        var source = opening && entranceDoorOpenAudio ? entranceDoorOpenAudio : entranceDoorAudio;
        // Independent voices let the opening creak finish even when the closing movement begins.
        return source && source.clip && !source.isPlaying ? StartCoroutine(EntranceDoorSound(source)) : null;
    }
    private IEnumerator EntranceDoorSound(AudioSource source)
    {
        float volume = source.volume;
        double started = AudioSettings.dspTime;
        float duration = source.clip.length / Mathf.Max(.01f, Mathf.Abs(source.pitch));
        source.volume = 0; source.Play();
        while (source && source.isPlaying)
        {
            float elapsed = (float)(AudioSettings.dspTime - started);
            float attack = Mathf.SmoothStep(0, 1, elapsed / .015f);
            float release = Mathf.SmoothStep(0, 1, (duration - elapsed) / Mathf.Min(.12f, duration * .25f));
            source.volume = volume * attack * release;
            yield return null;
        }
        if (source) source.volume = volume;
    }
    public void PlayDropImpact(Vector3 position, float speed)
    {
        if (dropImpacts == null || dropImpacts.Length == 0) return;
        int index = nextImpact++ % dropImpacts.Length;
        var source = dropImpacts[index]; if (!source || !source.clip) return;
        source.transform.position = position;
        source.volume = impactVolumes[index] * Mathf.Clamp(speed / 3f, .25f, 1f);
        source.Stop(); source.Play();
    }
    public void SetScriptedWalking(bool walking, Transform actor = null)
    {
        scriptedWalking = walking; scriptedWalker = walking ? actor : null; walkDistance = 0;
        var walker = scriptedWalker ? scriptedWalker : player ? player.transform : null;
        if (walker) lastPlayerPosition = walker.position;
    }
    private void BeginReading()
    {
        StopReading();
        if (reading && reading.clip) readingRoutine = StartCoroutine(ReadCard());
    }
    private IEnumerator ReadCard()
    {
        reading.volume = readingVolume; reading.loop = true; reading.Play();
        // Sustain under the punches, then fade smoothly to silence at ten seconds.
        for (float elapsed = 0; elapsed < 10f; elapsed += Time.unscaledDeltaTime)
        {
            reading.volume = readingVolume * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(8f, 10f, elapsed)));
            yield return null;
        }
        reading.Stop(); reading.volume = readingVolume; readingRoutine = null;
    }
    private void OnEjected(Pickupable item) => StopReading();
    private void StopReading()
    {
        if (readingRoutine != null) StopCoroutine(readingRoutine);
        readingRoutine = null;
        if (reading) { reading.Stop(); reading.volume = readingVolume; }
    }
    private void LateUpdate()
    {
        var walker = scriptedWalking && scriptedWalker ? scriptedWalker : player ? player.transform : null;
        if (!walker) return;
        Vector3 displacement = walker.position - lastPlayerPosition;
        lastPlayerPosition = walker.position; displacement.y = 0;
        bool walking = (scriptedWalking || player && player.isActiveAndEnabled && player.ControlEnabled && character && character.isGrounded) && displacement.sqrMagnitude < 4f;
        if (!walking || displacement.sqrMagnitude < .000001f) { walkDistance = 0; return; }
        walkDistance += displacement.magnitude;
        if (walkDistance < stepDistance || Time.unscaledTime < nextStepTime || !footsteps || !footsteps.clip || footsteps.isPlaying) return;
        walkDistance = 0;
        footsteps.transform.position = walker.position;
        footsteps.pitch = Random.Range(.96f, 1.04f);
        nextStepTime = Time.unscaledTime + Mathf.Max(stepInterval, footsteps.clip.length / Mathf.Abs(footsteps.pitch));
        footsteps.Play();
    }
    private void OnDisable()
    {
        if (lever) { lever.FeedStarted -= BeginReading; lever.CardEjected -= OnEjected; }
        StopAllCoroutines();
        scriptedWalking = false; scriptedWalker = null; walkDistance = 0;
        StopReading();
        foreach (var source in new[] { ambient, computerHum, cameraMove, footsteps, itemHandling, entranceDoorAudio, entranceDoorOpenAudio }) if (source) source.Stop();
        if (entranceDoorOpenAudio) entranceDoorOpenAudio.volume = entranceOpenVolume;
        if (entranceDoorAudio) entranceDoorAudio.volume = entranceCloseVolume;
        if (dropImpacts != null) foreach (var source in dropImpacts) if (source) source.Stop();
        if (scenes.TryGetValue(gameObject.scene.handle, out var current) && current == this) scenes.Remove(gameObject.scene.handle);
    }
}
