using UnityEngine;

/// <summary>A physical fader clamped to the black line on the recorder panel.</summary>
[DisallowMultipleComponent]
public sealed class RecorderSlider : MonoBehaviour, IInteractable, IDraggable, IScrollable, IPrimaryInteractable
{
    [SerializeField] private CassetteRecorder recorder;
    [SerializeField, Range(0, 5)] private int band;
    [SerializeField] private string bandLabel;
    [SerializeField] private Vector3 railStart;
    [SerializeField] private Vector3 railEnd;
    [SerializeField, Range(0f, 1f)] private float initialValue;
    [SerializeField, Min(30f)] private float pixelsForFullTravel = 250f;
    [SerializeField] private Material outlineMaterial;
    private Camera dragCamera;
    private MenuHoverOutline outline;
    private MaterialPropertyBlock outlineProperties;
    private bool focusedHover;
    private float pointerOffset;
    public float Value { get; private set; }
    public bool IsDragging { get; private set; }
    public int Band => band;
    public static string ControlLabel(int index) => index switch
    {
        0 => "Pitch", 1 => "Bass", 2 => "Mid", 3 => "Treble", 4 => "Denoise", 5 => "Fine Tune", _ => "Control"
    };
    public string ValueLabel => band switch
    {
        0 => $"{Mathf.Lerp(-12f, 12f, Value):+0.0;-0.0;0.0} st",
        1 or 2 or 3 => $"{RecorderSignalProcessor.ToneDecibels(Value):+0.0;-0.0;0.0} dB",
        _ => $"{Value:P0}"
    };
    public Vector3 WorldRailStart => transform.parent.TransformPoint(railStart);
    public Vector3 WorldRailEnd => transform.parent.TransformPoint(railEnd);

    private void Awake()
    {
        bandLabel = ControlLabel(band);
        Value = initialValue; ApplyPose();
        if (outlineMaterial)
        {
            outline = gameObject.AddComponent<MenuHoverOutline>();
            outline.Configure(outlineMaterial, GetComponent<Renderer>());
            outlineProperties = new MaterialPropertyBlock();
        }
    }
    public bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled && recorder && recorder.Current;
    public string GetPrompt(PlayerInteractor interactor) => recorder.InspectionView && recorder.InspectionView.IsActive
        ? $"[Drag / wheel] {bandLabel}: {ValueLabel} | Signal: {recorder.SignalQuality:P0} | [E] {(recorder.IsPlaying ? "Stop" : "Play / take")} | [Esc] Back"
        : recorder.GetPrompt(interactor);
    public bool CanPrimaryInteract(PlayerInteractor interactor) => recorder && recorder.CanPrimaryInteract(interactor);
    public void PrimaryInteract(PlayerInteractor interactor) => recorder.PrimaryInteract(interactor);
    public void Interact(PlayerInteractor interactor) => recorder.Interact(interactor);
    public void BeginDrag() { IsDragging = true; dragCamera = Camera.main; }
    public void EndDrag() { IsDragging = false; dragCamera = null; }
    public void BeginPointerDrag(Camera camera, Vector2 position)
    {
        IsDragging = true; dragCamera = camera;
        Vector2 head = camera.WorldToScreenPoint(transform.position);
        pointerOffset = Vector2.Distance(head, position) <= 20f ? Value - PointerValue(position) : 0f;
        DragPointer(position);
    }
    public void DragPointer(Vector2 position) { if (IsDragging && dragCamera) SetValue(PointerValue(position) + pointerOffset); }
    private float PointerValue(Vector2 position)
    {
        Vector2 start = dragCamera.WorldToScreenPoint(WorldRailStart), end = dragCamera.WorldToScreenPoint(WorldRailEnd);
        Vector2 axis = end - start;
        return Vector2.Dot(position - start, axis) / Mathf.Max(1f, axis.sqrMagnitude);
    }
    public void SetFocusedHover(bool value) => focusedHover = value;
    public void Drag(Vector2 mouseDelta)
    {
        if (!IsDragging || !recorder || !recorder.Current) return;
        Vector3 axis = transform.parent.TransformVector(railEnd - railStart).normalized;
        Vector2 screenAxis = Vector2.up;
        if (dragCamera)
            screenAxis = ((Vector2)(dragCamera.WorldToScreenPoint(transform.position + axis) - dragCamera.WorldToScreenPoint(transform.position))).normalized;
        SetValue(Value + Vector2.Dot(mouseDelta, screenAxis) / pixelsForFullTravel);
    }
    public void Scroll(int direction) { if (recorder && recorder.Current) SetValue(Value + direction * .05f); }
    public void SetValue(float value)
    {
        value = Mathf.Clamp01(value);
        if (band == 0 && Mathf.Abs(value - .5f) < .02f) value = .5f;
        if (Mathf.Approximately(Value, value)) return;
        Value = value;
        ApplyPose();
        if (recorder) recorder.SliderChanged();
    }
    private void ApplyPose() => transform.localPosition = Vector3.Lerp(railStart, railEnd, Value);
    private void LateUpdate()
    {
        if (!outline) return;
        var camera = Camera.main;
        bool visible = camera && (camera.transform.position - transform.position).sqrMagnitude < 144f;
        outline.SetHovered(visible);
        if (visible) outline.SetAppearance(focusedHover || IsDragging ? new Color(.45f, 1.2f, .65f) : new Color(.15f, .7f, .32f),
            focusedHover || IsDragging ? 1.7f : 1.2f, outlineProperties);
    }
    private void OnDisable() { EndDrag(); if (outline) outline.SetHovered(false); }
}
