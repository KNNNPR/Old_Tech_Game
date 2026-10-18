using System.Collections;
using UnityEngine;

/// <summary>Places a blank print under the enlarger, flashes its lamp and exposes it once.</summary>
[DisallowMultipleComponent]
public sealed class PhotoExposureStation : MonoBehaviour, IInteractable
{
    [SerializeField] private Transform paperAnchor;
    [SerializeField] private Light exposureLight;
    [SerializeField, Min(0)] private float placementSeconds = .4f;
    [SerializeField, Min(.02f)] private float flashSeconds = .14f;
    [SerializeField, Min(1)] private float flashMultiplier = 2.2f;

    private Color lampColor;
    private float lampIntensity;
    private bool lampEnabled;
    private Coroutine exposure;
    public DevelopingPhoto Current { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsFlashing { get; private set; }

    public string GetPrompt(PlayerInteractor interactor) => Current
        ? "[E] Take exposed photo" : "[E] Expose photo";

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (!isActiveAndEnabled || IsBusy || !paperAnchor) return false;
        if (Current) return interactor.HeldItem == null;
        var photo = interactor.HeldItem ? interactor.HeldItem.GetComponent<DevelopingPhoto>() : null;
        return photo && !photo.IsExposed && photo.HasImages;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        if (Current)
        {
            var item = Current.Item;
            Current = null;
            interactor.Hold(item);
            return;
        }
        var held = interactor.ReleaseHeld();
        Current = held.GetComponent<DevelopingPhoto>();
        held.AttachTo(paperAnchor, Vector3.zero, Quaternion.identity);
        IsBusy = true;
        exposure = StartCoroutine(Expose());
    }

    private IEnumerator Expose()
    {
        yield return new WaitForSeconds(placementSeconds);
        if (Current)
        {
            if (exposureLight)
            {
                lampColor = exposureLight.color;
                lampIntensity = exposureLight.intensity;
                lampEnabled = exposureLight.enabled;
                IsFlashing = true;
                exposureLight.enabled = true;
                exposureLight.color = new Color(1f, .92f, .8f);
                exposureLight.intensity = Mathf.Max(lampIntensity * flashMultiplier, 600f);
            }
            Current.Expose();
            yield return new WaitForSeconds(flashSeconds);
            RestoreLamp();
        }
        IsBusy = false;
        exposure = null;
    }

    private void RestoreLamp()
    {
        if (IsFlashing && exposureLight)
        {
            exposureLight.color = lampColor;
            exposureLight.intensity = lampIntensity;
            exposureLight.enabled = lampEnabled;
        }
        IsFlashing = false;
    }

    private void OnDisable()
    {
        if (exposure != null) StopCoroutine(exposure);
        exposure = null;
        RestoreLamp();
        IsBusy = false;
        if (!Current) return;
        Current.Item.Detach(Current.transform.position, Current.transform.rotation);
        Current = null;
    }
}
