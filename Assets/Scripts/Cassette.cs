using UnityEngine;

/// <summary>Кассета с записью, которую можно носить и вставлять в Recorder.</summary>
[RequireComponent(typeof(Pickupable))]
public class Cassette : MonoBehaviour
{
    [SerializeField] private AudioClip clip;
    [SerializeField] private AudioClip[] normalRecordings;
    [SerializeField] private AudioClip[] anomalyRecordings;
    private AudioClip carrier;
    private float[] tuningTargets;

    public bool IsAnomaly { get; private set; }
    public AudioClip CarrierClip => IsAnomaly ? carrier : clip;

    public AudioClip Clip
    {
        get => clip;
        set => AssignRecording(value, Contains(anomalyRecordings, value));
    }

    private void Awake()
    {
        if (clip) { AssignRecording(clip, Contains(anomalyRecordings, clip)); return; }
        bool evil = HasClips(anomalyRecordings) && (!HasClips(normalRecordings) || Random.value < .5f);
        AssignRecording(Choose(evil ? anomalyRecordings : normalRecordings), evil);
    }

    /// <param name="carrierClip">Для аномальной: что звучит поверх скрытой записи. Пусто — случайная обычная.</param>
    public void AssignRecording(AudioClip recording, bool anomaly, AudioClip carrierClip = null)
    {
        clip = recording;
        IsAnomaly = anomaly;
        carrier = anomaly ? (carrierClip ? carrierClip : Choose(normalRecordings)) : recording;
    }

    // A tape keeps its tuning after stop, removal and reinsertion.
    public void PrepareTuning(float[] initialValues)
    {
        if (tuningTargets != null) return;
        tuningTargets = new float[6];
        for (int i = 0; i < tuningTargets.Length; i++)
        {
            if (i == 4) { tuningTargets[i] = Random.Range(.65f, .9f); continue; }
            float initial = initialValues[i], target = Random.Range(.12f, .88f);
            if (Mathf.Abs(target - initial) < .25f) target = initial > .5f ? .16f : .84f;
            tuningTargets[i] = target;
        }
    }
    public float TuningTarget(int band) => tuningTargets != null ? tuningTargets[Mathf.Clamp(band, 0, 5)] : .5f;
    private static bool Contains(AudioClip[] clips, AudioClip value)
    {
        if (!value || clips == null) return false;
        foreach (var candidate in clips) if (candidate == value) return true;
        return false;
    }
    private static bool HasClips(AudioClip[] clips)
    {
        if (clips == null) return false;
        foreach (var candidate in clips) if (candidate) return true;
        return false;
    }
    private static AudioClip Choose(AudioClip[] clips)
    {
        if (!HasClips(clips)) return null;
        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
            if (clips[(start + i) % clips.Length]) return clips[(start + i) % clips.Length];
        return null;
    }
}