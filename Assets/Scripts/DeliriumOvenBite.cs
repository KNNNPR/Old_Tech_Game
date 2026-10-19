using System.Collections;
using UnityEngine;

/// <summary>One authored tooth closure and reopening per accepted ritual press.</summary>
public sealed class DeliriumOvenBite : MonoBehaviour
{
    [SerializeField] private Transform upper, lower;
    [SerializeField] private Vector3 upperClosedOffset, lowerClosedOffset;
    [SerializeField] private AudioSource metalAudio;
    [SerializeField, Min(.1f)] private float duration = .28f;
    public bool IsPlaying { get; private set; }
    public int BiteCount { get; private set; }
    private Vector3 upperRest, lowerRest;
    private bool initialized;
    private void Awake()
    {
        if (upper) upperRest = upper.localPosition;
        if (lower) lowerRest = lower.localPosition;
        initialized = true;
    }
    public bool Bite()
    {
        if (IsPlaying || !upper || !lower) return false;
        StartCoroutine(Animate()); return true;
    }
    private IEnumerator Animate()
    {
        IsPlaying = true;
        bool sounded = false;
        const float closure = .42f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float amount = t < closure ? Mathf.SmoothStep(0,1,t/closure) : 1-Mathf.SmoothStep(0,1,(t-closure)/(1-closure));
            upper.localPosition = upperRest + upperClosedOffset * amount;
            lower.localPosition = lowerRest + lowerClosedOffset * amount;
            if (!sounded && t >= closure)
            {
                sounded = true; BiteCount++;
                if (metalAudio && metalAudio.clip) metalAudio.PlayOneShot(metalAudio.clip);
            }
            yield return null;
        }
        // Still produce the closure cue if a stalled frame skipped the middle of the animation.
        if (!sounded) { BiteCount++; if (metalAudio && metalAudio.clip) metalAudio.PlayOneShot(metalAudio.clip); }
        Restore();
    }
    private void Restore()
    {
        if (!initialized) return;
        if (upper) upper.localPosition = upperRest;
        if (lower) lower.localPosition = lowerRest;
        IsPlaying = false;
    }
    private void OnDisable() { StopAllCoroutines(); if (metalAudio) metalAudio.Stop(); Restore(); }
}
