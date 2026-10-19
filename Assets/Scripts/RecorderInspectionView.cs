using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A fixed close view with a free pointer and full-rail fader selection.</summary>
[DisallowMultipleComponent]
public sealed class RecorderInspectionView : MonoBehaviour
{
    [SerializeField] private CassetteRecorder recorder;
    [SerializeField] private Transform cameraPose;
    [SerializeField] private RecorderSlider[] sliders;
    [SerializeField] private RecorderPlayButton playButton;
    [SerializeField, Range(25f, 65f)] private float focusedFieldOfView = 43f;
    [SerializeField, Min(.1f)] private float transitionSeconds = .55f;
    [SerializeField, Min(12f)] private float selectionRadius = 27f;
    private PlayerInteractor player;
    private FirstPersonController controller;
    private Camera viewCamera;
    private Vector3 savedLocalPosition, savedWorldPosition, transitionPosition;
    private Quaternion savedLocalRotation, savedWorldRotation, transitionRotation;
    private float savedFov, transitionFov, transitionTime;
    private bool returning, controllerEnabled, controlEnabled, inputEnabled, savedCursorVisible;
    private CursorLockMode savedCursorLock;
    private RecorderSlider dragging, hovered;
    public bool IsActive { get; private set; }
    public bool IsTransitioning { get; private set; }
    public bool IsConfigured
    {
        get
        {
            if (!recorder || !cameraPose || !playButton || sliders == null || sliders.Length != 6) return false;
            foreach (var slider in sliders) if (!slider) return false;
            return true;
        }
    }

    public void Enter(PlayerInteractor interactor)
    {
        if (IsActive || !isActiveAndEnabled || !IsConfigured || !recorder.Current || !interactor || interactor.HasExternalInteraction) return;
        player = interactor; viewCamera = player.PlayerCamera; controller = player.Controller;
        if (!viewCamera) return;
        GameplaySceneAudio.For(this)?.PlayCameraMove();
        savedLocalPosition = viewCamera.transform.localPosition; savedLocalRotation = viewCamera.transform.localRotation;
        savedWorldPosition = viewCamera.transform.position; savedWorldRotation = viewCamera.transform.rotation; savedFov = viewCamera.fieldOfView;
        savedCursorVisible = Cursor.visible; savedCursorLock = Cursor.lockState; inputEnabled = player.InputEnabled;
        if (controller)
        {
            controllerEnabled = controller.enabled; controlEnabled = controller.ControlEnabled;
            controller.SetControlEnabled(false); controller.enabled = false;
        }
        IsActive = true; returning = false;
        player.InputEnabled = false;
        player.BeginExternalInteraction(Tick);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        StartTransition();
    }

    public void Exit()
    {
        if (!IsActive || returning) return;
        if (dragging) dragging.EndDrag();
        dragging = null; SetHover(null);
        returning = true; StartTransition();
    }
    private void StartTransition()
    {
        transitionPosition = viewCamera.transform.position; transitionRotation = viewCamera.transform.rotation;
        transitionFov = viewCamera.fieldOfView; transitionTime = 0f; IsTransitioning = true;
        player.SetExternalPrompt(null, "");
    }
    private void Tick()
    {
        if (!IsActive) return;
        var keyboard = Keyboard.current; var mouse = Mouse.current;
        if (!recorder || !recorder.Current || keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Exit();
        if (IsTransitioning)
        {
            transitionTime += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transitionTime / transitionSeconds));
            viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(transitionPosition, returning ? savedWorldPosition : cameraPose.position, t),
                Quaternion.Slerp(transitionRotation, returning ? savedWorldRotation : cameraPose.rotation, t));
            viewCamera.fieldOfView = Mathf.Lerp(transitionFov, returning ? savedFov : focusedFieldOfView, t);
            player.SetExternalPrompt(null, "");
            if (transitionTime >= transitionSeconds)
            {
                IsTransitioning = false;
                if (returning) Restore();
            }
            return;
        }
        // Keep the view completely steady while the pointer manipulates the physical panel.
        viewCamera.transform.SetPositionAndRotation(cameraPose.position, cameraPose.rotation);
        if (mouse != null)
        {
            Vector2 pointer = mouse.position.ReadValue();
            SetHover(dragging ? dragging : PickSlider(pointer));
            if (dragging)
            {
                if (mouse.leftButton.isPressed) dragging.DragPointer(pointer);
                else { dragging.EndDrag(); dragging = null; }
            }
            else
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (hovered) { dragging = hovered; dragging.BeginPointerDrag(viewCamera, pointer); }
                    else if (OverButton(pointer)) recorder.TogglePlayback();
                }
                if (hovered && Mathf.Abs(mouse.scroll.ReadValue().y) > .01f) hovered.Scroll(mouse.scroll.ReadValue().y > 0 ? 1 : -1);
            }
        }
        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            recorder.Interact(player);
            if (!recorder.Current) Exit();
        }
        player.SetExternalPrompt(hovered ? (IInteractable)hovered : recorder,
            hovered ? hovered.GetPrompt(player) : recorder.InspectionPrompt);
    }

    public RecorderSlider PickSlider(Vector2 pointer)
    {
        RecorderSlider nearest = null; float distance = selectionRadius;
        if (!viewCamera || sliders == null) return null;
        foreach (var slider in sliders)
        {
            if (!slider || !slider.isActiveAndEnabled) continue;
            Vector3 a = viewCamera.WorldToScreenPoint(slider.WorldRailStart), b = viewCamera.WorldToScreenPoint(slider.WorldRailEnd);
            if (a.z <= 0 || b.z <= 0) continue;
            Vector2 axis = (Vector2)(b - a);
            float t = Mathf.Clamp01(Vector2.Dot(pointer - (Vector2)a, axis) / Mathf.Max(1f, axis.sqrMagnitude));
            float candidate = Vector2.Distance(pointer, (Vector2)a + axis * t);
            if (candidate < distance) { nearest = slider; distance = candidate; }
        }
        return nearest;
    }
    private bool OverButton(Vector2 pointer)
    {
        if (!playButton) return false;
        Vector3 point = viewCamera.WorldToScreenPoint(playButton.transform.position);
        return point.z > 0 && Vector2.Distance(pointer, point) <= 35f;
    }
    private void SetHover(RecorderSlider value)
    {
        if (hovered == value) return;
        if (hovered) hovered.SetFocusedHover(false);
        hovered = value;
        if (hovered) hovered.SetFocusedHover(true);
    }
    private void Restore()
    {
        if (dragging) dragging.EndDrag(); dragging = null; SetHover(null);
        if (viewCamera) { viewCamera.transform.localPosition = savedLocalPosition; viewCamera.transform.localRotation = savedLocalRotation; viewCamera.fieldOfView = savedFov; }
        if (player) { player.EndExternalInteraction(Tick); player.InputEnabled = inputEnabled; }
        if (controller) { controller.enabled = controllerEnabled; controller.SetControlEnabled(controlEnabled); }
        Cursor.lockState = savedCursorLock; Cursor.visible = savedCursorVisible;
        IsActive = IsTransitioning = returning = false;
    }
    private void OnDisable() { if (IsActive) Restore(); }
}
