using System.Collections;
using UnityEngine;

/// <summary>One shot of the authored oven clip and fire, only when its bin accepts a card.</summary>
public sealed class OvenIncineration : MonoBehaviour
{
    [SerializeField] private SortBin bin;
    [SerializeField] private AnimationClip incinerate;
    [SerializeField] private Animator ovenAnimator;
    [SerializeField] private Transform[] animatedParts;
    [SerializeField] private ParticleSystem flames;
    [SerializeField] private ParticleSystem sparks;
    [SerializeField] private Light fireLight;
    [SerializeField, Min(0)] private float lightIntensity = 7;
    [SerializeField] private AudioSource fireAudio, biteAudio;
    [SerializeField] private float[] biteTimes;
    [SerializeField, Min(.1f)] private float fireFadeSeconds = 2f;

    public bool IsPlaying { get; private set; }
    public float Duration => incinerate ? incinerate.length : 0;
    private Vector3[] restPositions;
    private bool animatorWasEnabled;
    private float fireVolume;

    private void OnEnable()
    {
        if (fireAudio) fireVolume = fireAudio.volume;
        restPositions = new Vector3[animatedParts == null ? 0 : animatedParts.Length];
        for (int i = 0; i < restPositions.Length; i++)
            if (animatedParts[i]) restPositions[i] = animatedParts[i].localPosition;
        if (ovenAnimator)
        {
            animatorWasEnabled = ovenAnimator.enabled;
            // The controller's default state loops; this component plays the clip once on demand.
            ovenAnimator.enabled = false;
        }
        StopFire();
        if (biteAudio) biteAudio.Stop();
        if (bin) bin.ProcessingStarted += Begin;
    }

    private void OnDisable()
    {
        if (bin) bin.ProcessingStarted -= Begin;
        StopAllCoroutines();
        Restore();
        if (biteAudio) biteAudio.Stop();
        if (ovenAnimator) ovenAnimator.enabled = animatorWasEnabled;
    }

    private void Begin()
    {
        if (!incinerate || IsPlaying) return;
        StartCoroutine(Incinerate());
    }

    private IEnumerator Incinerate()
    {
        IsPlaying = true;
        if (fireAudio && fireAudio.clip) { fireAudio.volume = fireVolume; fireAudio.loop = true; fireAudio.Play(); }
        if (flames) { flames.Play(); flames.Emit(18); }
        if (sparks) { sparks.Play(); sparks.Emit(8); }
        if (fireLight) fireLight.enabled = true;
        float duration = Mathf.Max(.01f, Duration);
        bool stoppedEmission = false;
        int nextBite = 0;
        for (float time = 0; time < duration; time += Time.deltaTime)
        {
            incinerate.SampleAnimation(gameObject, time);
            while (biteTimes != null && nextBite < biteTimes.Length && time >= biteTimes[nextBite])
            {
                if (biteAudio && biteAudio.clip) { biteAudio.Stop(); biteAudio.Play(); }
                nextBite++;
            }
            if (fireAudio)
                fireAudio.volume = fireVolume * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Mathf.Max(0, duration - fireFadeSeconds), duration, time)));
            float envelope = Mathf.SmoothStep(0, 1, time / .1f) * Mathf.SmoothStep(0, 1, (duration - time) / .28f);
            if (fireLight) fireLight.intensity = lightIntensity * envelope * (.72f + .28f * Mathf.PerlinNoise(time * 24, 5));
            if (!stoppedEmission && duration - time < .25f)
            {
                if (flames) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                stoppedEmission = true;
            }
            yield return null;
        }
        incinerate.SampleAnimation(gameObject, duration);
        Restore();
    }

    private void StopFire()
    {
        if (fireAudio) { fireAudio.Stop(); fireAudio.volume = fireVolume; }
        if (flames) flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (sparks) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (fireLight) { fireLight.intensity = 0; fireLight.enabled = false; }
    }

    private void Restore()
    {
        StopFire();
        if (restPositions != null)
            for (int i = 0; i < restPositions.Length; i++)
                if (animatedParts[i]) animatedParts[i].localPosition = restPositions[i];
        IsPlaying = false;
    }
}
