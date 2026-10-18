using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Взаимодействие игрока.
///
/// Обычный режим: луч из центра экрана. E — взаимодействие, G — положить предмет,
/// ЛКМ — начать работу за компьютером (FocusStation) или тянуть ползунок.
///
/// Режим работы за компьютером: камера зафиксирована, виден курсор, луч идёт из-под курсора.
/// ЛКМ — нажать или тянуть элемент экрана, Esc — выйти.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Camera playerCamera;
    [Tooltip("Пустой объект, дочерний к камере: где предмет висит в руке.")]
    [SerializeField] private Transform holdPoint;
    [Tooltip("Контроллер игрока. Если пусто — ищется на этом же объекте.")]
    [SerializeField] private FirstPersonController controller;

    [Header("Параметры")]
    [SerializeField] private float interactDistance = 2f;
    [SerializeField] private LayerMask interactMask = ~0;
    [SerializeField] private float dropDistance = 0.7f;

    [Header("Работа за компьютером")]
    [SerializeField] private float focusTransitionTime = 0.5f;

    public Pickupable HeldItem { get; private set; }
    public IInteractable CurrentTarget { get; private set; }
    public string CurrentPrompt { get; private set; } = "";

    /// <summary>Отключай на время катсцен.</summary>
    public bool InputEnabled { get; set; } = true;

    public FocusStation CurrentFocus { get; private set; }
    public bool IsFocused => CurrentFocus != null;

    private IDraggable dragging;
    private Coroutine cameraRoutine;
    private bool cameraLockedToFocus;
    private Vector3 cameraRestLocalPosition;
    private Quaternion cameraRestLocalRotation;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        if (controller == null) controller = GetComponent<FirstPersonController>();
        cameraRestLocalPosition = playerCamera.transform.localPosition;
        cameraRestLocalRotation = playerCamera.transform.localRotation;
    }

    private void Update()
    {
        if (externalInteraction != null) { externalInteraction(); return; }
        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;

        // Пока тянем — только перетаскивание.
        if (dragging != null)
        {
            if (InputEnabled && mouse != null && mouse.leftButton.isPressed)
                dragging.Drag(mouse.delta.ReadValue());
            else
                StopDragging();

            // A moving physical fader can leave the crosshair while it is being dragged.
            // Keep its readout visible until the mouse button is released.
            if (dragging is IInteractable draggedTarget)
            {
                CurrentTarget = draggedTarget;
                CurrentPrompt = draggedTarget.GetPrompt(this);
            }
            else FindTarget();
            return;
        }

        FindTarget();

        if (!InputEnabled) return;

        if (IsFocused && kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            ExitFocus();
            return;
        }

        // ЛКМ: тянуть ползунок, сесть за компьютер, нажать элемент экрана.
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && CurrentTarget != null)
        {
            if (CurrentTarget is IDraggable draggable)
            {
                StartDragging(draggable);
                return;
            }

            if (IsFocused || CurrentTarget is FocusStation)
            {
                CurrentTarget.Interact(this);
                return;
            }
        }

        // Колесо мыши для ползунков и ручек.
        if (mouse != null && CurrentTarget is IScrollable scrollable)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                scrollable.Scroll(scroll > 0f ? 1 : -1);
        }

        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame && CurrentTarget != null)
            CurrentTarget.Interact(this);
        else if (kb.gKey.wasPressedThisFrame && HeldItem != null)
            DropHeld();
    }

    private void LateUpdate()
    {
        // Камера стоит в точке фокуса, даже если голова «покачивается».
        if (cameraLockedToFocus && CurrentFocus != null)
        {
            Transform point = CurrentFocus.FocusPoint;
            playerCamera.transform.SetPositionAndRotation(point.position, point.rotation);
        }
    }

    // ---------- Поиск цели ----------

    private void FindTarget()
    {
        CurrentTarget = null;
        CurrentPrompt = "";

        if (!InputEnabled) return;

        Ray ray;
        Mouse mouse = Mouse.current;
        if (IsFocused && mouse != null)
            ray = playerCamera.ScreenPointToRay(mouse.position.ReadValue());
        else
            ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        int count = Physics.RaycastNonAlloc(ray, hits, interactDistance, interactMask, QueryTriggerInteraction.Collide);

        // Ближайшее попадание, пропуская элементы экрана, если мы не за компьютером.
        float bestDistance = float.MaxValue;
        IInteractable best = null;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            IInteractable interactable = hits[i].collider.GetComponentInParent<IInteractable>();

            if (!IsFocused && interactable is IRequiresFocus) continue;

            // За компьютером коллайдер самого экрана не должен закрывать его элементы.
            if (IsFocused && ReferenceEquals(interactable, CurrentFocus)) continue;

            if (hits[i].distance < bestDistance)
            {
                bestDistance = hits[i].distance;
                best = interactable;
                found = true;
            }
        }

        if (found && best != null && best.CanInteract(this))
        {
            CurrentTarget = best;
            CurrentPrompt = best.GetPrompt(this);
            return;
        }

        if (IsFocused)
            CurrentPrompt = "[Esc] Step away";
        else if (HeldItem != null)
            CurrentPrompt = "[G] Drop";
    }

    // ---------- Работа за компьютером ----------

    public void EnterFocus(FocusStation station)
    {
        if (IsFocused || station == null) return;

        CurrentFocus = station;
        station.SetFocused(true);

        if (controller != null) controller.SetControlEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        cameraRoutine = StartCoroutine(MoveCameraToFocus(station.FocusPoint));
    }

    public void ExitFocus()
    {
        if (!IsFocused) return;
        if (dragging != null) StopDragging();

        cameraLockedToFocus = false;
        CurrentFocus.SetFocused(false);
        CurrentFocus = null;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraRoutine != null) StopCoroutine(cameraRoutine);
        cameraRoutine = StartCoroutine(ReturnCamera());
    }

    private IEnumerator MoveCameraToFocus(Transform point)
    {
        Transform cam = playerCamera.transform;
        Vector3 fromPosition = cam.position;
        Quaternion fromRotation = cam.rotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, focusTransitionTime))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            cam.SetPositionAndRotation(
                Vector3.Lerp(fromPosition, point.position, s),
                Quaternion.Slerp(fromRotation, point.rotation, s));
            yield return null;
        }

        cameraLockedToFocus = true;
        cameraRoutine = null;
    }

    private IEnumerator ReturnCamera()
    {
        Transform cam = playerCamera.transform;
        Vector3 fromPosition = cam.localPosition;
        Quaternion fromRotation = cam.localRotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, focusTransitionTime))
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            cam.localPosition = Vector3.Lerp(fromPosition, cameraRestLocalPosition, s);
            cam.localRotation = Quaternion.Slerp(fromRotation, cameraRestLocalRotation, s);
            yield return null;
        }

        cam.localPosition = cameraRestLocalPosition;
        cam.localRotation = cameraRestLocalRotation;

        if (controller != null) controller.SetControlEnabled(true);
        cameraRoutine = null;
    }

    // ---------- Перетаскивание ----------

    private void StartDragging(IDraggable draggable)
    {
        dragging = draggable;
        dragging.BeginDrag();
        if (controller != null) controller.SetControlEnabled(false);
    }

    private void StopDragging()
    {
        dragging.EndDrag();
        dragging = null;

        // За компьютером управление остаётся выключенным до выхода по Esc.
        if (!IsFocused && controller != null) controller.SetControlEnabled(true);
    }

    // ---------- Предметы ----------

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
