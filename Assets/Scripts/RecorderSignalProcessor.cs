using System;
using UnityEngine;

/// <summary>Three tone controls, gentle tape hiss, denoising and receiver clarity. No Unity APIs on the audio thread.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
public sealed class RecorderSignalProcessor : MonoBehaviour
{
    private readonly float[] targetGains = { 1, 1, 1 };
    private readonly float[] gains = { 1, 1, 1 };
    private readonly float[] history = new float[6 * 8];
    private readonly float[] envelope = new float[8];
    private volatile float noiseLevel, noiseReduction, outputRms;
    private volatile float bassCoefficient, midCoefficient, cleanCoefficient, tuningLowCoefficient, tuningHighCoefficient;
    private volatile bool processing;
    private volatile bool resetHistory = true;
    private int sampleRate = 48000;
    private float smoothedNoise, smoothedReduction, cleanPole, tuningLowPole, tuningHighPole;
    private float clarity = 1f;
    private uint randomState = 8191;
    public float OutputRms => outputRms;

    private void Awake() => ConfigureRate();
    private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
    private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
    private void OnAudioConfigurationChanged(bool changed) => ConfigureRate();
    private float Coefficient(float hz) => (float)(1d - Math.Exp(-2d * Math.PI * Math.Min(hz, sampleRate * .4f) / sampleRate));
    private void ConfigureRate()
    {
        sampleRate = Math.Max(8000, AudioSettings.outputSampleRate);
        bassCoefficient = Coefficient(300f);
        midCoefficient = Coefficient(3000f);
        SetNoiseReduction(noiseReduction);
        SetClarity(clarity);
    }
    public static float ToneDecibels(float value) => value < .5f
        ? Mathf.Lerp(-12f, 0f, Mathf.Clamp01(value) * 2f)
        : Mathf.Lerp(0f, 9f, (Mathf.Clamp01(value) - .5f) * 2f);
    public void SetTone(float bass, float mid, float treble)
    {
        targetGains[0] = Mathf.Pow(10f, ToneDecibels(bass) / 20f);
        targetGains[1] = Mathf.Pow(10f, ToneDecibels(mid) / 20f);
        targetGains[2] = Mathf.Pow(10f, ToneDecibels(treble) / 20f);
    }
    public void SetNoiseReduction(float amount)
    {
        noiseReduction = Mathf.Clamp01(amount);
        cleanCoefficient = Coefficient(Mathf.Lerp(14000f, 3400f, noiseReduction));
    }
    public void SetClarity(float amount)
    {
        clarity = Mathf.Clamp01(amount);
        // Away from the station, only a narrow, muffled part of the hidden recording gets through.
        tuningLowCoefficient = Coefficient(Mathf.Lerp(650f, 30f, clarity));
        tuningHighCoefficient = Coefficient(Mathf.Lerp(1100f, 12000f, clarity));
    }
    public void SetNoise(float amount) => noiseLevel = Mathf.Clamp(amount, 0f, .001f);
    public void SetProcessing(bool active)
    {
        processing = active;
        if (!active) { noiseLevel = 0f; outputRms = 0f; resetHistory = true; }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (resetHistory)
        {
            Array.Clear(history, 0, history.Length);
            Array.Clear(envelope, 0, envelope.Length);
            smoothedNoise = 0f;
            resetHistory = false;
        }
        // Stopped sources must not synthesize hiss or release stored filter tails.
        if (!processing || channels < 1 || channels > 8)
        {
            Array.Clear(data, 0, data.Length);
            Array.Clear(history, 0, history.Length);
            Array.Clear(envelope, 0, envelope.Length);
            smoothedNoise = 0f;
            outputRms = 0f;
            return;
        }
        double energy = 0;
        for (int frame = 0; frame < data.Length; frame += channels)
        {
            // Smooth once per frame, so stereo channels receive identical control changes.
            for (int band = 0; band < 3; band++) gains[band] += (targetGains[band] - gains[band]) * .001f;
            smoothedReduction += (noiseReduction - smoothedReduction) * .001f;
            smoothedNoise += (noiseLevel - smoothedNoise) * .001f;
            cleanPole += (cleanCoefficient - cleanPole) * .001f;
            tuningLowPole += (tuningLowCoefficient - tuningLowPole) * .001f;
            tuningHighPole += (tuningHighCoefficient - tuningHighPole) * .001f;
            for (int channel = 0; channel < channels && frame + channel < data.Length; channel++)
            {
                int index = frame + channel;
                float input = data[index];
                float low = history[channel] += bassCoefficient * (input - history[channel]);
                float middle = history[8 + channel] += midCoefficient * (input - history[8 + channel]);
                float result = low * gains[0] + (middle - low) * gains[1] + (input - middle) * gains[2];

                // Two gentle low-pass stages and a soft downward expander also reduce hiss in the clip itself.
                float clean = history[16 + channel] += cleanPole * (result - history[16 + channel]);
                clean = history[24 + channel] += cleanPole * (clean - history[24 + channel]);
                float level = Math.Abs(result);
                envelope[channel] += (level - envelope[channel]) * (level > envelope[channel] ? .02f : .0004f);
                float gate = .2f + .8f * Math.Min(1f, envelope[channel] / .008f);
                result += (clean * gate - result) * smoothedReduction;

                float lowCut = history[32 + channel] += tuningLowPole * (result - history[32 + channel]);
                result = history[40 + channel] += tuningHighPole * (result - lowCut - history[40 + channel]);
                randomState ^= randomState << 13; randomState ^= randomState >> 17; randomState ^= randomState << 5;
                result += ((randomState & 65535) / 32767.5f - 1f) * smoothedNoise * (1f - smoothedReduction * .95f);
                // Leave output headroom even with all three tone controls boosted.
                result = .8f * (float)Math.Tanh(result * .85f);
                data[index] = result;
                energy += result * result;
            }
        }
        outputRms = data.Length > 0 ? (float)Math.Sqrt(energy / data.Length) : 0f;
    }
}
