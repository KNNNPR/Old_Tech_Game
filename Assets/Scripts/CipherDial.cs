using System;
using UnityEngine;

/// <summary>
/// Ползунок сдвига шифра на экране монитора.
/// Основное управление: навести прицел, зажать ЛКМ и вести мышь влево-вправо.
/// Запасное: колесо мыши (шаг ±1) или E (шаг +1).
/// Активен, только пока на экране зашифрованный текст.
/// </summary>
public class CipherDial : MonoBehaviour, IInteractable, IScrollable, IDraggable
{
    [Header("Значения")]
    [SerializeField] private int minValue = 0;
    [Tooltip("В английском алфавите 26 букв, поэтому осмысленных сдвигов 25.")]
    [SerializeField] private int maxValue = 25;
    [Tooltip("Колесо и E: после максимума перескакивать на минимум. При перетаскивании не действует.")]
    [SerializeField] private bool wrap = true;
    [SerializeField] private string label = "Сдвиг";

    [Header("Перетаскивание")]
    [Tooltip("Сколько пикселей движения мыши нужно на один шаг ползунка. Больше — точнее и медленнее.")]
    [SerializeField] private float pixelsPerStep = 12f;

    [Header("Визуал ползунка (необязательно)")]
    [Tooltip("Всё, что видно у ползунка (дорожка, ручка, подпись). Прячется, пока шифра нет.")]
    [SerializeField] private GameObject visual;
    [Tooltip("Подвижная часть ползунка.")]
    [SerializeField] private Transform knob;
    [SerializeField] private Vector3 knobMinLocalPosition;
    [SerializeField] private Vector3 knobMaxLocalPosition;
    [SerializeField] private float knobSpeed = 15f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip stepClip;

    public int Value { get; private set; }
    public int MinValue => minValue;
    public int MaxValue => maxValue;
    public bool IsActive { get; private set; }
    public bool IsDragging { get; private set; }

    public event Action<int> ValueChanged;

    private Collider[] colliders;
    private float dragAccumulator;

    private void Awake()
    {
        Value = minValue;
        SetActive(IsActive);
    }

    private void Update()
    {
        if (knob == null || maxValue == minValue) return;

        float t = Mathf.InverseLerp(minValue, maxValue, Value);
        Vector3 target = Vector3.Lerp(knobMinLocalPosition, knobMaxLocalPosition, t);
        knob.localPosition = Vector3.Lerp(knob.localPosition, target, knobSpeed * Time.deltaTime);
    }

    public void SetActive(bool active)
    {
        if (colliders == null) colliders = GetComponents<Collider>();

        IsActive = active;
        if (visual != null) visual.SetActive(active);
        foreach (Collider c in colliders) c.enabled = active;
    }

    /// <summary>Вернуть ползунок в начало без события.</summary>
    public void ResetValue() => Value = minValue;

    public void Step(int delta) => Step(delta, wrap);

    private void Step(int delta, bool allowWrap)
    {
        int range = maxValue - minValue + 1;
        int next = Value + delta;

        next = allowWrap
            ? minValue + (((next - minValue) % range) + range) % range
            : Mathf.Clamp(next, minValue, maxValue);

        if (next == Value) return;

        Value = next;
        if (audioSource != null && stepClip != null) audioSource.PlayOneShot(stepClip);
        ValueChanged?.Invoke(Value);
    }

    // ---------- Перетаскивание ----------

    public void BeginDrag()
    {
        IsDragging = true;
        dragAccumulator = 0f;
    }

    public void Drag(Vector2 mouseDelta)
    {
        if (!IsActive) return;

        dragAccumulator += mouseDelta.x / Mathf.Max(1f, pixelsPerStep);

        // Каждые pixelsPerStep пикселей — один шаг. Без перескока через край:
        // ползунок упирается в конец дорожки, как настоящий.
        while (dragAccumulator >= 1f)
        {
            Step(+1, false);
            dragAccumulator -= 1f;
        }
        while (dragAccumulator <= -1f)
        {
            Step(-1, false);
            dragAccumulator += 1f;
        }
    }

    public void EndDrag()
    {
        IsDragging = false;
        dragAccumulator = 0f;
    }

    // ---------- IInteractable / IScrollable ----------

    public string GetPrompt(PlayerInteractor interactor) =>
        IsDragging ? $"{label}: {Value:00}" : $"[ЛКМ, тянуть] {label}: {Value:00}";

    public bool CanInteract(PlayerInteractor interactor) => IsActive;

    public void Interact(PlayerInteractor interactor) => Step(+1);

    public void Scroll(int direction) => Step(direction);

    // Удобные кнопки в меню компонента (⋮): поставь ручку в край и запомни положение.
    [ContextMenu("Запомнить текущее положение ручки как MIN")]
    private void CaptureMin()
    {
        if (knob != null) knobMinLocalPosition = knob.localPosition;
    }

    [ContextMenu("Запомнить текущее положение ручки как MAX")]
    private void CaptureMax()
    {
        if (knob != null) knobMaxLocalPosition = knob.localPosition;
    }
}