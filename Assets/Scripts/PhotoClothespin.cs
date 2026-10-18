using UnityEngine;

/// <summary>A single drying clip; its hitbox also covers the hanging print for taking it down.</summary>
[DisallowMultipleComponent]
public sealed class PhotoClothespin : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform paperAnchor;
    public DevelopingPhoto Current { get; private set; }

    public string GetPrompt(PlayerInteractor interactor) => Current
        ? "[E] Take photo" : "[E] Hang photo to develop";

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || !paperAnchor) return false;
        if (Current) return interactor.HeldItem == null;
        var photo = interactor.HeldItem ? interactor.HeldItem.GetComponent<DevelopingPhoto>() : null;
        return photo && photo.IsExposed;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (Current)
        {
            var photo = Current;
            Current = null;
            photo.TakeDown();
            interactor.Hold(photo.Item);
        }
        else
        {
            var item = interactor.ReleaseHeld();
            Current = item.GetComponent<DevelopingPhoto>();
            item.AttachTo(paperAnchor, Vector3.zero, Quaternion.identity);
            Current.HangOn(this);
        }
    }

    private void OnDisable()
    {
        if (!Current) return;
        Current.TakeDown();
        Current.Item.Detach(Current.transform.position, Current.transform.rotation);
        Current = null;
    }
}
