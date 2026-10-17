using UnityEngine;

/// <summary>
/// Предмет, который можно взять в руку, положить или вставить в гнездо.
/// Пока предмет в руке или в гнезде, его физика и коллайдеры выключены.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Pickupable : MonoBehaviour, IInteractable
{
    [SerializeField] private string displayName = "предмет";

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

        float t = attachSpeed * Time.deltaTime;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, t);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetLocalRotation, t);
    }

    public string GetPrompt(PlayerInteractor interactor) => $"[E] Взять: {displayName}";

    public bool CanInteract(PlayerInteractor interactor) => !IsAttached && interactor.HeldItem == null;

    public void Interact(PlayerInteractor interactor) => interactor.Hold(this);

    /// <summary>Прикрепить к руке или гнезду, плавно переместив в заданную точку.</summary>
    public void AttachTo(Transform parent, Vector3 localPosition, Quaternion localRotation)
    {
        IsAttached = true;
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
        body.isKinematic = !enabled;
        foreach (Collider c in colliders)
            c.enabled = enabled;
    }
}
