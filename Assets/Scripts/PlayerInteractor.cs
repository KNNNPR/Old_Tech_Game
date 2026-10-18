using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Луч из центра камеры, поиск IInteractable, E — взаимодействие, G — положить предмет.
/// Хранит предмет в руке.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Пустой объект, дочерний к камере: где предмет висит в руке.")]
    [SerializeField] private Transform holdPoint;

    [Header("Параметры")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private float dropDistance = 0.7f;

    [Tooltip("Контроллер игрока: на время перетаскивания обзор замораживается. Если пусто — ищется на этом же объекте.")]
    [SerializeField] private FirstPersonController controller;

    public Pickupable HeldItem { get; private set; }
    public IInteractable CurrentTarget { get; private set; }
    public string CurrentPrompt { get; private set; } = "";

    /// <summary>Отключай на время режима станции или катсцен.</summary>
    public bool InputEnabled { get; set; } = true;

    private IDraggable dragging;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;

        // Пока тянем — только перетаскивание, всё остальное ждёт.
        if (dragging != null)
        {
            if (InputEnabled && mouse != null && mouse.leftButton.isPressed)
                dragging.Drag(mouse.delta.ReadValue());
            else
                StopDragging();

            FindTarget();
            return;
        }

        FindTarget();

        if (!InputEnabled) return;

        // ЛКМ по ползунку — начать перетаскивание.
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && CurrentTarget is IDraggable draggable)
        {
            dragging = draggable;
            dragging.BeginDrag();
            if (controller != null) controller.SetControlEnabled(false);
            return;
        }

        // Колесо мыши для ползунков и ручек под прицелом.
        if (mouse != null && CurrentTarget is IScrollable scrollable)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                scrollable.Scroll(scroll > 0f ? 1 : -1);
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame && CurrentTarget != null)
            CurrentTarget.Interact(this);
        else if (kb.gKey.wasPressedThisFrame && HeldItem != null)
            DropHeld();
    }

    private void FindTarget()
    {
        CurrentTarget = null;
        CurrentPrompt = "";

        if (!InputEnabled) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactMask, QueryTriggerInteraction.Collide))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null && interactable.CanInteract(this))
            {
                CurrentTarget = interactable;
                CurrentPrompt = interactable.GetPrompt(this);
                return;
            }
        }

        if (HeldItem != null)
            CurrentPrompt = "[G] Положить";
    }

    private void StopDragging()
    {
        dragging.EndDrag();
        dragging = null;
        if (controller != null) controller.SetControlEnabled(true);
    }

    /// <summary>Взять предмет в руку.</summary>
    public void Hold(Pickupable item)
    {
        if (HeldItem != null || item == null) return;

        HeldItem = item;
        item.AttachTo(holdPoint, item.HoldPositionOffset, Quaternion.Euler(item.HoldRotationOffset));
    }

    /// <summary>
    /// Забрать предмет из руки, не бросая его в мир.
    /// Используют гнёзда: они сами решают, куда предмет прикрепить.
    /// </summary>
    public Pickupable ReleaseHeld()
    {
        Pickupable item = HeldItem;
        HeldItem = null;
        return item;
    }

    private void DropHeld()
    {
        Pickupable item = ReleaseHeld();
        Transform cam = playerCamera.transform;

        Vector3 position = cam.position + cam.forward * dropDistance;
        if (Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, dropDistance, interactMask, QueryTriggerInteraction.Ignore))
            position = hit.point - cam.forward * 0.1f;

        Quaternion rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);
        item.Detach(position, rotation);
    }
}