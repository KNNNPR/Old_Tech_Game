using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Предмет в руке «живёт»: отстаёт от поворота мыши, покачивается в такт шагам и чуть
/// опускается при ходьбе. Вешается на HoldPoint (пустой объект под камерой, где висит предмет).
/// Меняет только сам HoldPoint — предметы, слоты и камеру не трогает.
/// </summary>
public class HoldSway : MonoBehaviour
{
    [Header("Отставание от мыши")]
    [Tooltip("Насколько градусов предмет отстаёт при повороте мышью.")]
    [SerializeField] private float swayDegrees = 4f;
    [Tooltip("Сдвиг предмета при повороте (м).")]
    [SerializeField] private float swayShift = 0.015f;
    [SerializeField] private float swaySmooth = 8f;

    [Header("Ходьба")]
    [Tooltip("Покачивание при ходьбе (м).")]
    [SerializeField] private float walkBob = 0.012f;
    [SerializeField] private float walkFrequency = 1.6f;
    [Tooltip("Насколько предмет опускается при ходьбе (м).")]
    [SerializeField] private float walkDrop = 0.01f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector2 sway;
    private float walkTimer;
    private float walk01;
    private Vector3 lastPosition;
    private Transform body;
    private PlayerInteractor interactor;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        interactor = GetComponentInParent<PlayerInteractor>();
        var controller = GetComponentInParent<CharacterController>();
        body = controller != null ? controller.transform : transform.root;
        lastPosition = body.position;
    }

    private void LateUpdate()
    {
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // Мышь: только в обычном режиме (курсор захвачен, не за компьютером).
        Vector2 target = Vector2.zero;
        Mouse mouse = Mouse.current;
        bool free = Cursor.lockState == CursorLockMode.Locked && (interactor == null || (!interactor.IsFocused && interactor.InputEnabled));
        if (free && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            target = new Vector2(Mathf.Clamp(delta.x / dt / 600f, -1f, 1f), Mathf.Clamp(delta.y / dt / 600f, -1f, 1f));
        }
        sway = Vector2.Lerp(sway, target, 1f - Mathf.Exp(-swaySmooth * dt));

        // Ходьба: по реальной скорости тела.
        Vector3 velocity = (body.position - lastPosition) / dt;
        lastPosition = body.position;
        velocity.y = 0f;
        float targetWalk = free ? Mathf.Clamp01(velocity.magnitude / 2.2f) : 0f;
        walk01 = Mathf.Lerp(walk01, targetWalk, 1f - Mathf.Exp(-6f * dt));
        walkTimer += dt * walkFrequency * walk01;
        float phase = walkTimer * Mathf.PI * 2f;

        Vector3 offset = new Vector3(
            -sway.x * swayShift + Mathf.Cos(phase * 0.5f) * walkBob * walk01,
            -sway.y * swayShift + Mathf.Abs(Mathf.Sin(phase)) * walkBob * walk01 - walkDrop * walk01,
            0f);
        Quaternion tilt = Quaternion.Euler(sway.y * swayDegrees, -sway.x * swayDegrees, sway.x * swayDegrees * 0.5f);

        transform.localPosition = basePosition + offset;
        transform.localRotation = baseRotation * tilt;
    }
}
