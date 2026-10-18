using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Feeds a blank photo out of the printer and holds it at the outlet until the player takes it.</summary>
[DisallowMultipleComponent]
public sealed class PhotoPrinterSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject photoPrefab;
    [SerializeField] private Transform feedStart;
    [SerializeField] private Transform outputPose;
    [Tooltip("Pickup trigger in front of the surrounding machine's coarse collision box.")]
    [SerializeField] private Collider pickupTarget;
    [SerializeField, Min(.1f)] private float feedSeconds = 1.6f;
    [Header("Temporary automatic printing; turn off when gameplay events call TryPrintPhoto")]
    [SerializeField] private bool automaticPrinting = true;
    [SerializeField, Min(0)] private float initialDelay = 2f;
    [SerializeField, Min(.1f)] private float printInterval = 8f;
    [SerializeField, Min(1)] private int maxLoosePhotos = 3;

    private readonly List<DevelopingPhoto> issued = new List<DevelopingPhoto>();
    private DevelopingPhoto feeding;
    private Rigidbody feedingBody;
    private Collider[] feedingColliders;
    private bool[] colliderStates;
    private float timer, progress;
    private bool ready;
    public bool IsPrinting => feeding != null;
    public DevelopingPhoto OutputPhoto { get; private set; }
    public DevelopingPhoto LastPrintedPhoto { get; private set; }
    public event Action<DevelopingPhoto> PhotoReady;

    // The printer's coarse authored collider overlaps part of the sheet; allow taking the ready print there too.
    public string GetPrompt(PlayerInteractor interactor) => "[E] Take photo";
    public bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled && !IsPrinting && OutputPhoto
        && !OutputPhoto.Item.IsAttached && interactor.HeldItem == null;
    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        var item = OutputPhoto.Item;
        OutputPhoto = null;
        SetPickupTarget(false);
        timer = printInterval;
        interactor.Hold(item);
    }

    private void Awake()
    {
        ready = photoPrefab && photoPrefab.GetComponent<DevelopingPhoto>() && photoPrefab.GetComponent<Pickupable>()
            && photoPrefab.GetComponent<Rigidbody>() && feedStart && outputPose;
        timer = initialDelay;
        SetPickupTarget(false);
        if (!ready) Debug.LogError("PhotoPrinterSpawner needs the Photo prefab and its two feed poses.", this);
    }

    private void Update()
    {
        if (!ready) return;
        if (feeding)
        {
            progress = Mathf.Min(1, progress + Time.deltaTime / feedSeconds);
            MoveSheet(progress);
            if (progress >= 1) CompletePrint();
            return;
        }
        if (OutputPhoto && (!OutputPhoto.gameObject.activeInHierarchy || OutputPhoto.Item.IsAttached))
        {
            OutputPhoto = null;
            SetPickupTarget(false);
            timer = printInterval;
        }
        if (!automaticPrinting || OutputPhoto) return;
        timer -= Time.deltaTime;
        if (timer <= 0 && !TryPrintPhoto()) timer = .5f;
    }

    /// <summary>Call from future gameplay events. False means the printer is busy, occupied or at the loose-print limit.</summary>
    public bool TryPrintPhoto()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !ready || IsPrinting) return false;
        // A pickup may have happened this frame, before this component's Update.
        if (OutputPhoto && OutputPhoto.Item.IsAttached) OutputPhoto = null;
        if (OutputPhoto && OutputPhoto.gameObject.activeInHierarchy) return false;
        issued.RemoveAll(photo => !photo);
        int loose = 0;
        foreach (var photo in issued)
            if (photo.gameObject.activeInHierarchy && !photo.Item.IsAttached) loose++;
        if (loose >= maxLoosePhotos) return false;

        var instance = Instantiate(photoPrefab, feedStart.position, feedStart.rotation);
        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        feeding = instance.GetComponent<DevelopingPhoto>();
        SetPickupTarget(false);
        feedingBody = instance.GetComponent<Rigidbody>();
        feedingBody.interpolation = RigidbodyInterpolation.None;
        feedingBody.isKinematic = true;
        // Keep gravity enabled so Pickupable.Detach restores an ordinary physical photo when it is dropped.
        feedingColliders = instance.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[feedingColliders.Length];
        for (int i = 0; i < feedingColliders.Length; i++)
        {
            colliderStates[i] = feedingColliders[i].enabled;
            feedingColliders[i].enabled = false;
        }
        issued.Add(feeding);
        LastPrintedPhoto = feeding;
        progress = 0;
        timer = printInterval;
        MoveSheet(0);
        return true;
    }

    private void MoveSheet(float amount)
    {
        feedingBody.position = Vector3.Lerp(feedStart.position, outputPose.position, amount);
        feedingBody.rotation = Quaternion.Slerp(feedStart.rotation, outputPose.rotation, amount);
    }

    private void CompletePrint()
    {
        var photo = feeding;
        if (!photo || !feedingBody) { feeding = null; return; }
        MoveSheet(1);
        for (int i = 0; i < feedingColliders.Length; i++)
            if (feedingColliders[i]) feedingColliders[i].enabled = colliderStates[i];
        OutputPhoto = photo;
        SetPickupTarget(true);
        feeding = null;
        feedingBody = null;
        feedingColliders = null;
        colliderStates = null;
        PhotoReady?.Invoke(photo);
    }

    private void OnDisable()
    {
        if (feeding) CompletePrint();
        SetPickupTarget(false);
    }

    private void SetPickupTarget(bool value)
    {
        if (pickupTarget) pickupTarget.enabled = value;
    }

    [ContextMenu("Print Photo")]
    private void PrintFromInspector() => TryPrintPhoto();

    private void OnDrawGizmosSelected()
    {
        if (!feedStart || !outputPose) return;
        Gizmos.color = new Color(.4f, 1, .6f);
        Gizmos.DrawLine(feedStart.position, outputPose.position);
        Gizmos.DrawWireSphere(outputPose.position, .05f);
    }
}
