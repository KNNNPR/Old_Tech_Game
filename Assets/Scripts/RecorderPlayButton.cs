using UnityEngine;

/// <summary>The physical red transport button also restarts a stopped tape.</summary>
[DisallowMultipleComponent]
public sealed class RecorderPlayButton : MonoBehaviour, IInteractable, IPrimaryInteractable
{
    [SerializeField] private CassetteRecorder recorder;
    public bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled && recorder && recorder.Current;
    public string GetPrompt(PlayerInteractor interactor) => recorder.IsPlaying ? "[E] Stop tape" : "[E] Play tape";
    public void Interact(PlayerInteractor interactor) { if (CanInteract(interactor)) recorder.TogglePlayback(); }
    public bool CanPrimaryInteract(PlayerInteractor interactor) => recorder && recorder.CanPrimaryInteract(interactor);
    public void PrimaryInteract(PlayerInteractor interactor) => recorder.PrimaryInteract(interactor);
}
