using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Финал дня 4. Игрок вставляет руку (предмет с FinalHand) в слот компьютера:
/// монитор «считывает» руку, камера слегка трясётся, затем быстро влетает в экран,
/// у самого экрана — чернота и следующий шаг сюжета (THE_END).
/// Вешается на любой объект основной сцены.
/// </summary>
// Позже остальных скриптов: наша поза камеры перекрывает игрока, фокус компьютера и покачивание головы.
[DefaultExecutionOrder(1000)]
public class FinalHandTrigger : MonoBehaviour
{
    [Tooltip("Слот компьютера, куда вставляются карты.")]
    [SerializeField] private ItemSlot computerSlot;

    [Header("Экран")]
    [Tooltip("Точка в центре экрана монитора (ScreenCenter). Камера летит в неё.")]
    [SerializeField] private Transform screenTarget;
    [Tooltip("Монитор — напечатать «считывание». Если пусто — найдётся сам.")]
    [SerializeField] private MonitorDisplay monitor;
    [TextArea, SerializeField] private string readingText = "HAND DETECTED.\nREADING...";

    [Header("Тряска")]
    [SerializeField] private float shakeSeconds = 1.8f;
    [Tooltip("Сила тряски в градусах. Нарастает к концу.")]
    [SerializeField] private float shakeDegrees = 1.5f;

    [Header("Влёт в экран")]
    [SerializeField] private float flySeconds = 0.7f;
    [Tooltip("Насколько не долетать до экрана (метры) — там уже полная чернота.")]
    [SerializeField] private float stopDistance = 0.02f;
    [Tooltip("С какой доли пути начинается затемнение.")]
    [SerializeField, Range(0f, 1f)] private float fadeFrom = 0.75f;
    [Tooltip("На сколько градусов расширяется обзор при влёте (ощущение скорости).")]
    [SerializeField] private float fovKick = 15f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Когда компьютер принимает руку.")]
    [SerializeField] private AudioClip acceptClip;
    [Tooltip("Гул во время тряски.")]
    [SerializeField] private AudioClip shakeClip;
    [Tooltip("Влёт в экран.")]
    [SerializeField] private AudioClip flyInClip;

    public UnityEvent onHandInserted;

    private bool triggered;
    private bool cameraLocked;
    private Transform cam;
    private Vector3 posePosition;
    private Quaternion poseRotation;
    private CanvasGroup black;

    private void Awake()
    {
        if (monitor == null) monitor = FindFirstObjectByType<MonitorDisplay>();
    }

    private void OnEnable()
    {
        if (computerSlot != null) computerSlot.ItemInserted += OnItemInserted;
        else Debug.LogError("FinalHandTrigger: не задан слот компьютера.", this);
    }

    private void OnDisable()
    {
        if (computerSlot != null) computerSlot.ItemInserted -= OnItemInserted;
    }

    private void LateUpdate()
    {
        if (cameraLocked && cam != null) cam.SetPositionAndRotation(posePosition, poseRotation);
    }

    private void OnItemInserted(Pickupable item)
    {
        if (triggered || item == null || !item.TryGetComponent(out FinalHand _)) return;
        triggered = true;
        StartCoroutine(FinalRoutine());
    }

    private IEnumerator FinalRoutine()
    {
        // Руку уже не вынуть, игрок больше ничего не трогает.
        computerSlot.Locked = true;
        var interactor = FindFirstObjectByType<PlayerInteractor>();
        var controller = FindFirstObjectByType<FirstPersonController>();
        if (interactor != null) interactor.InputEnabled = false;
        if (controller != null) controller.SetControlEnabled(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Play(acceptClip);
        onHandInserted?.Invoke();
        if (monitor != null) monitor.ShowSystemText(readingText);

        if (interactor == null || interactor.PlayerCamera == null || screenTarget == null)
        {
            Debug.LogError("FinalHandTrigger: нет камеры игрока или не задан Screen Target — сразу перехожу дальше.", this);
            yield return new WaitForSeconds(shakeSeconds);
            GameFlow.Advance();
            yield break;
        }

        Camera view = interactor.PlayerCamera;
        cam = view.transform;
        Vector3 startPosition = posePosition = cam.position;
        Quaternion startRotation = poseRotation = cam.rotation;
        float startFov = view.fieldOfView;
        black = CreateBlackOverlay();
        cameraLocked = true;

        // 1. Лёгкая нарастающая тряска на месте.
        Play(shakeClip);
        for (float t = 0f; t < shakeSeconds; t += Time.deltaTime)
        {
            float strength = shakeDegrees * Mathf.Lerp(0.4f, 1f, t / shakeSeconds);
            float n = Time.time * 22f;
            poseRotation = startRotation * Quaternion.Euler(
                (Mathf.PerlinNoise(n, 0.1f) - 0.5f) * 2f * strength,
                (Mathf.PerlinNoise(0.4f, n) - 0.5f) * 2f * strength,
                (Mathf.PerlinNoise(n, 0.8f) - 0.5f) * strength);
            yield return null;
        }

        // 2. Быстрый влёт в экран. Конечное направление — «внутрь экрана»:
        // синяя ось ScreenCenter (как у холста монитора), если она смотрит от игрока, иначе — прямо на экран.
        Vector3 toScreen = (screenTarget.position - startPosition).normalized;
        Vector3 into = Vector3.Dot(screenTarget.forward, toScreen) > 0.2f ? screenTarget.forward : toScreen;
        Vector3 endPosition = screenTarget.position - into * stopDistance;
        Quaternion endRotation = Quaternion.LookRotation(into, Vector3.up);
        Quaternion fromRotation = poseRotation;

        Play(flyInClip);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.05f, flySeconds))
        {
            float move = t * t * t;                                   // разгон — основное движение в конце
            float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 2.5f)); // разворот к экрану — в начале
            posePosition = Vector3.Lerp(startPosition, endPosition, move);
            poseRotation = Quaternion.Slerp(fromRotation, endRotation, turn);
            view.fieldOfView = startFov + fovKick * move;
            black.alpha = Mathf.Clamp01((t - fadeFrom) / Mathf.Max(0.01f, 1f - fadeFrom));
            yield return null;
        }

        posePosition = endPosition;
        black.alpha = 1f;
        yield return new WaitForSeconds(0.3f);
        GameFlow.Advance();
    }

    private CanvasGroup CreateBlackOverlay()
    {
        var canvasObject = new GameObject("Final Overlay", typeof(Canvas), typeof(CanvasGroup));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(canvasObject.transform, false);
        var rect = image.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.GetComponent<Image>().color = Color.black;
        image.GetComponent<Image>().raycastTarget = false;

        var group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        return group;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}