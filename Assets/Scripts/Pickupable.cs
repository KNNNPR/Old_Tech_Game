using UnityEngine;

/// <summary>
/// Предмет, который можно взять в руку, положить или вставить в гнездо.
/// Пока предмет в руке или в гнезде, его физика и коллайдеры выключены.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pickupable : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "item";

    [Header("Положение в руке")]
    [SerializeField] private Vector3 holdPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 holdRotationOffset = Vector3.zero;

    [Tooltip("Скорость, с которой предмет плавно подлетает в руку или в гнездо.")]
    [SerializeField] private float attachSpeed = 14f;

    public string DisplayName => displayName;
    public Vector3 HoldPositionOffset => holdPositionOffset;
    public Vector3 HoldRotationOffset => holdRotationOffset;

    /// <summary>Предмет сейчас в руке или в гнезде.</summary>
    public bool IsAttached { get; private set; }

    private Rigidbody body;
    private Collider[] colliders;
    private Vector3 targetLocalPosition;
    private Quaternion targetLocalRotation;
    private float currentAttachSpeed;
    private float nextImpactSound;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        // Тонкие предметы (перфокарты, письма) иначе пролетают сквозь пол и стол.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void Update()
    {
        if (!IsAttached) return;

        float t = currentAttachSpeed * Time.deltaTime;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, t);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, t);
    }

    public string GetPrompt(PlayerInteractor interactor) => $"[E] Pick up: {displayName}";

    public bool CanInteract(PlayerInteractor interactor) => !IsAttached && interactor.HeldItem == null;

    public void Interact(PlayerInteractor interactor) => interactor.Hold(this);

    /// <summary>
    /// Прикрепить к руке или гнезду, плавно переместив в заданную точку.
    /// speed > 0 — своя скорость подлёта (например, медленный выезд из принтера).
    /// </summary>
    public void AttachTo(Transform parent, Vector3 localPosition, Quaternion localRotation, float speed = 0f)
    {
        IsAttached = true;
        currentAttachSpeed = speed > 0f ? speed : attachSpeed;
        SetPhysics(false);

        transform.SetParent(parent, true);
        targetLocalPosition = localPosition;
        targetLocalRotation = localRotation;
    }

    /// <summary>Отпустить в мир с включённой физикой.</summary>
    public void Detach(Vector3 worldPosition, Quaternion worldRotation)
    {
        IsAttached = false;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        SetPhysics(true);
    }

    private void SetPhysics(bool enabled)
    {
        // Rigidbody interpolation otherwise writes the previous world pose over the attachment animation.
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = !enabled;
        if (enabled)
        {
            body.position = transform.position;
            body.rotation = transform.rotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }
        foreach (Collider c in colliders)
            c.enabled = enabled;
    }
    private void OnCollisionEnter(Collision collision)
    {
        float speed = collision.relativeVelocity.magnitude;
        if (IsAttached || body.isKinematic || speed < .6f || Time.unscaledTime < nextImpactSound) return;
        nextImpactSound = Time.unscaledTime + .25f;
        Vector3 position = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        GameplaySceneAudio.For(this)?.PlayDropImpact(position, speed);
    }
}
