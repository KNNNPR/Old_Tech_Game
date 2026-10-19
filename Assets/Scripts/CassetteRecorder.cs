using UnityEngine;

/// <summary>Reversible cassette slot with pitch, three tone controls, denoising and hidden-signal tuning.</summary>
[DisallowMultipleComponent]
public sealed class CassetteRecorder : MonoBehaviour, IInteractable, IPrimaryInteractable
{
    [SerializeField] private Transform cassetteAnchor;
    [SerializeField] private Transform bigCoil;
    [SerializeField] private Transform smallCoil;
    [SerializeField] private Transform playButton;
    [SerializeField] private Vector3 buttonPressOffset;
    [SerializeField] private RecorderSlider[] sliders;
    [SerializeField] private AudioSource carrierSource;
    [SerializeField] private AudioSource hiddenSource;
    [SerializeField] private AudioSource buttonSource;
    [SerializeField] private AudioSource insertSource;
    [SerializeField] private AudioClip buttonClip;
    [SerializeField] private RecorderSignalProcessor carrierProcessor;
    [SerializeField] private RecorderSignalProcessor hiddenProcessor;
    [SerializeField, Range(0f, 1f)] private float playbackVolume = .7f;
    [SerializeField] private RecorderInspectionView inspectionView;
    private bool hasPlayed, touchedSliders;
    private float reveal, revealVelocity, reelSpeed, buttonTimer, buttonSoundTimer;
    private Vector3 buttonRest;
    public Cassette Current { get; private set; }
    public bool IsPlaying { get; private set; }
    public float SignalQuality { get; private set; }
    public float RevealedSignal => reveal;
    public float PitchSemitones => Mathf.Lerp(-12f, 12f, ControlValue(0));
    public float FineTuneQuality => Current ? Mathf.Clamp01(1f - Mathf.Abs(ControlValue(5) - Current.TuningTarget(5)) / .22f) : 0f;
    public RecorderInspectionView InspectionView => inspectionView;
    public bool HasInspectionSetup => inspectionView && inspectionView.isActiveAndEnabled && inspectionView.IsConfigured;

    private void Awake()
    {
        GameSettings.Load();
        foreach (var source in new[] { carrierSource, hiddenSource, buttonSource, insertSource })
            if (source) { source.ignoreListenerVolume = false; source.spatialBlend = 1; source.dopplerLevel = 0; }
        if (!inspectionView) inspectionView = GetComponent<RecorderInspectionView>();
        if (playButton) buttonRest = playButton.localPosition;
        StopPlayback();
    }
    public string GetPrompt(PlayerInteractor interactor) => !Current ? "[E] Insert cassette"
        : (HasInspectionSetup ? "[LMB] Inspect recorder | " : "") + (IsPlaying ? "[E] Stop tape"
        : hasPlayed ? "[E] Take cassette" : "[E] Play tape");
    public bool CanPrimaryInteract(PlayerInteractor interactor) => Current && HasInspectionSetup && !inspectionView.IsActive;
    public void PrimaryInteract(PlayerInteractor interactor) { if (CanPrimaryInteract(interactor)) inspectionView.Enter(interactor); }
    public string InspectionPrompt => IsPlaying ? "[E] Stop tape | Drag faders / mouse wheel | [Esc] Back"
        : hasPlayed ? "[E] Take cassette | Red button: replay | [Esc] Back" : "[E] Play tape | Drag faders / mouse wheel | [Esc] Back";
    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !cassetteAnchor) return false;
        if (Current) return IsPlaying || !interactor.HeldItem;
        return interactor.HeldItem && interactor.HeldItem.GetComponent<Cassette>();
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (!Current)
        {
            var item = interactor.ReleaseHeld();
            Current = item.GetComponent<Cassette>();
            item.AttachTo(cassetteAnchor, Vector3.zero, Quaternion.identity);
            hasPlayed = touchedSliders = false;
            reveal = revealVelocity = 0f;
            var values = new float[6];
            for (int i = 0; i < values.Length; i++) values[i] = sliders != null && i < sliders.Length && sliders[i] ? sliders[i].Value : .5f;
            Current.PrepareTuning(values);
            RefreshQuality();
            if (insertSource && insertSource.clip) insertSource.PlayOneShot(insertSource.clip);
        }
        else if (IsPlaying || !hasPlayed) TogglePlayback();
        else
        {
            StopPlayback();
            var item = Current.GetComponent<Pickupable>();
            Current = null;
            SignalQuality = 0f;
            interactor.Hold(item);
        }
    }

    public void TogglePlayback()
    {
        if (!Current || !isActiveAndEnabled) return;
        if (IsPlaying) { StopPlayback(); PressButton(); return; }
        PressButton();
        if (!Current.Clip || !carrierSource || !hiddenSource) return;
        hasPlayed = IsPlaying = true;
        reveal = revealVelocity = 0f;
        carrierSource.enabled = hiddenSource.enabled = true;
        if (carrierProcessor) carrierProcessor.SetProcessing(true);
        if (hiddenProcessor) hiddenProcessor.SetProcessing(Current.IsAnomaly);
        carrierSource.clip = Current.CarrierClip;
        carrierSource.volume = playbackVolume;
        hiddenSource.clip = Current.IsAnomaly ? Current.Clip : null;
        hiddenSource.volume = 0f;
        UpdateProcessors();
        if (carrierSource.clip) carrierSource.Play();
        if (hiddenSource.clip) hiddenSource.Play();
    }
    public void StopPlayback()
    {
        IsPlaying = false;
        if (carrierProcessor) carrierProcessor.SetProcessing(false);
        if (hiddenProcessor) hiddenProcessor.SetProcessing(false);
        if (carrierSource) { carrierSource.Stop(); carrierSource.volume = 0f; carrierSource.clip = null; carrierSource.enabled = false; }
        if (hiddenSource) { hiddenSource.Stop(); hiddenSource.volume = 0f; hiddenSource.clip = null; hiddenSource.enabled = false; }
        if (carrierSource) carrierSource.pitch = 1f;
        if (hiddenSource) hiddenSource.pitch = 1f;
        if (buttonSource) buttonSource.Stop();
        buttonSoundTimer = 0f;
        reveal = revealVelocity = 0f;
        if (hiddenSource) hiddenSource.volume = 0f;
    }
    public void SliderChanged()
    {
        if (!Current) return;
        touchedSliders = true;
        RefreshQuality();
    }
    private void RefreshQuality()
    {
        if (!Current || sliders == null || sliders.Length != 6) { SignalQuality = 0f; return; }
        float score = 0f;
        // Fine Tune is the strongest clue; tone and pitch make the station clearer as it is decoded.
        score += Mathf.Clamp01(1f - Mathf.Abs(ControlValue(0) - Current.TuningTarget(0)) / .65f) * .2f;
        for (int i = 1; i <= 3; i++)
            score += Mathf.Clamp01(1f - Mathf.Abs(ControlValue(i) - Current.TuningTarget(i)) / .65f) * .1f;
        score += Mathf.Clamp01(ControlValue(4) / Current.TuningTarget(4)) * .15f;
        SignalQuality = score + FineTuneQuality * .35f;
    }
    private void Update()
    {
        if (buttonSoundTimer > 0f)
        {
            buttonSoundTimer -= Time.deltaTime;
            if (buttonSoundTimer <= 0f && buttonSource) buttonSource.Stop();
        }
        if (IsPlaying)
        {
            if (!Current) StopPlayback();
            else
            {
                float target = touchedSliders && Current.IsAnomaly
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, .92f, SignalQuality)) * Mathf.SmoothStep(0f, 1f, FineTuneQuality) : 0f;
                reveal = Mathf.SmoothDamp(reveal, target, ref revealVelocity, .18f);
                carrierSource.volume = playbackVolume * (1f - reveal * .85f);
                hiddenSource.volume = playbackVolume * reveal;
                float pitch = Mathf.Pow(2f, PitchSemitones / 12f);
                float blend = 1f - Mathf.Exp(-Time.deltaTime / .08f);
                carrierSource.pitch = Mathf.Lerp(carrierSource.pitch, pitch, blend);
                hiddenSource.pitch = Mathf.Lerp(hiddenSource.pitch, pitch, blend);
                UpdateProcessors();
            }
        }
        reelSpeed = Mathf.MoveTowards(reelSpeed, IsPlaying ? 1f : 0f, Time.deltaTime * 4f);
        if (bigCoil) bigCoil.Rotate(Vector3.up, 90f * reelSpeed * Time.deltaTime, Space.Self);
        if (smallCoil) smallCoil.Rotate(Vector3.up, 162f * reelSpeed * Time.deltaTime, Space.Self);
        if (playButton)
        {
            buttonTimer = Mathf.Max(0f, buttonTimer - Time.deltaTime);
            float elapsed = .22f - buttonTimer;
            float press = buttonTimer <= 0f ? 0f : elapsed < .06f ? elapsed / .06f : 1f - Mathf.Clamp01((elapsed - .08f) / .14f);
            playButton.localPosition = buttonRest + buttonPressOffset * press;
        }
    }
    private void UpdateProcessors()
    {
        ConfigureProcessor(carrierProcessor, 1f, .0006f);
        ConfigureProcessor(hiddenProcessor, FineTuneQuality, 0f);
    }
    private void ConfigureProcessor(RecorderSignalProcessor processor, float clarity, float hiss)
    {
        if (!processor) return;
        processor.SetTone(ControlValue(1), ControlValue(2), ControlValue(3));
        processor.SetNoiseReduction(ControlValue(4));
        processor.SetClarity(clarity);
        processor.SetNoise(hiss);
    }
    private float ControlValue(int index) => sliders != null && index < sliders.Length && sliders[index] ? sliders[index].Value : .5f;
    private void PressButton()
    {
        buttonTimer = .22f;
        if (buttonSource && buttonClip)
        {
            buttonSource.Stop();
            buttonSource.PlayOneShot(buttonClip, .08f);
            buttonSoundTimer = .1f;
        }
    }
    private void OnDisable()
    {
        StopPlayback();
        reelSpeed = buttonTimer = 0f;
        if (playButton) playButton.localPosition = buttonRest;
        if (!Current) return;
        Current.GetComponent<Pickupable>().Detach(Current.transform.position, Current.transform.rotation);
        Current = null;
    }
}
