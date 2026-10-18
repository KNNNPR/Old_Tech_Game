using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Контроллер от первого лица: ходьба, обзор мышью, гравитация, покачивание головы.
/// Без прыжка и бега: для медленного хоррора в одной комнате они не нужны.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Объект камеры (или пустой объект-родитель камеры) на уровне глаз.")]
    [SerializeField] private Transform cameraPivot;

    [Header("Движение")]
    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private float acceleration = 10f;
    [SerializeField] private float gravity = -20f;

    [Header("Обзор")]
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Покачивание головы")]
    [SerializeField] private float bobFrequency = 1.6f;
    [SerializeField] private float bobAmplitude = 0.035f;
    [SerializeField] private float bobReturnSpeed = 8f;

    public bool ControlEnabled { get; private set; } = true;

    private CharacterController characterController;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private float pitch;
    private float bobTimer;
    private Vector3 cameraBaseLocalPosition;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        cameraBaseLocalPosition = cameraPivot.localPosition;
    }

    private void OnEnable() => LockCursor(true);

    private void Update()
    {
        HandleCursor();

        if (ControlEnabled)
        {
            Look();
            Move();
        }
        else
        {
            horizontalVelocity = Vector3.zero;
            ApplyGravityOnly();
        }

        HeadBob();
    }

    /// <summary>
    /// Включает и выключает управление. Пригодится для режима станции
    /// (камера на перфораторе) и катсцен.
    /// </summary>
    public void SetControlEnabled(bool enabled)
    {
        ControlEnabled = enabled;
        if (!enabled) horizontalVelocity = Vector3.zero;
    }

    private void Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void Move()
    {
        Vector2 input = Vector2.zero;
        if (GameSettings.IsPressed(GameAction.Forward)) input.y += 1f;
        if (GameSettings.IsPressed(GameAction.Backward)) input.y -= 1f;
        if (GameSettings.IsPressed(GameAction.Right)) input.x += 1f;
        if (GameSettings.IsPressed(GameAction.Left)) input.x -= 1f;

        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 target = (transform.right * input.x + transform.forward * input.y) * walkSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, acceleration * Time.deltaTime);

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(motion * Time.deltaTime);
    }

    private void ApplyGravityOnly()
    {
        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        verticalVelocity += gravity * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void HeadBob()
    {
        float speed01 = horizontalVelocity.magnitude / walkSpeed;
        Vector3 targetPosition = cameraBaseLocalPosition;

        if (characterController.isGrounded && speed01 > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency * speed01;
            float phase = bobTimer * Mathf.PI * 2f;
            targetPosition += new Vector3(
                Mathf.Cos(phase * 0.5f) * bobAmplitude * 0.5f,
                Mathf.Sin(phase) * bobAmplitude,
                0f) * speed01;
        }
        else
        {
            bobTimer = 0f;
        }

        cameraPivot.localPosition = Vector3.Lerp(
            cameraPivot.localPosition, targetPosition, bobReturnSpeed * Time.deltaTime);
    }

    private void HandleCursor()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            LockCursor(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
