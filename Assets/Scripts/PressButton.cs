using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Универсальная физическая кнопка: E — нажать.
/// Что она делает, настраивается в инспекторе в списке On Pressed
/// (например, SortBin.Process у приёмника карт).
/// </summary>
public class PressButton : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "[E] Нажать";

    [Header("Анимация (необязательно)")]
    [Tooltip("Подвижная часть кнопки.")]
    [SerializeField] private Transform buttonCap;
    [Tooltip("Насколько кнопка утапливается (в координатах её родителя).")]
    [SerializeField] private Vector3 pressOffset = new Vector3(0f, -0.01f, 0f);
    [SerializeField] private float pressTime = 0.08f;
    [Tooltip("Защита от двойного нажатия.")]
    [SerializeField] private float cooldown = 0.5f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip clickClip;

    [Header("Действие")]
    public UnityEvent onPressed;

    private bool busy;
    private Vector3 capRest;

    private void Awake()
    {
        if (buttonCap != null) capRest = buttonCap.localPosition;
    }

    public string GetPrompt(PlayerInteractor interactor) => prompt;

    public bool CanInteract(PlayerInteractor interactor) => !busy;

    public void Interact(PlayerInteractor interactor) => StartCoroutine(PressRoutine());

    private IEnumerator PressRoutine()
    {
        busy = true;

        if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);

        if (buttonCap != null) buttonCap.localPosition = capRest + pressOffset;
        onPressed?.Invoke();
        yield return new WaitForSeconds(pressTime);
        if (buttonCap != null) buttonCap.localPosition = capRest;

        yield return new WaitForSeconds(cooldown);
        busy = false;
    }
}
