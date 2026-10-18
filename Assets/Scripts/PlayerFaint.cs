using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Игрок теряет сознание: управление отключается, камера заваливается и падает,
/// экран мигает, как веки, и уходит в черноту. Потом — следующая сцена.
/// Запускается MonitorDisplay, когда на экран попадает карта с Causes Blackout.
/// Вешается на любой объект основной сцены (удобно — на Player). Затемнение создаёт само.
/// </summary>
// Позже остальных скриптов: наша поза камеры должна перекрыть фокус компьютера и покачивание головы.
[DefaultExecutionOrder(1000)]
public class PlayerFaint : MonoBehaviour
{
    [Header("Ссылки (если пусто — найдутся сами)")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;

    [Header("Падение")]
    [SerializeField] private float fallSeconds = 2.6f;
    [Tooltip("Насколько опускается камера (метры).")]
    [SerializeField] private float dropHeight = 1.3f;
    [Tooltip("Завал головы вбок (градусы).")]
    [SerializeField] private float rollDegrees = 75f;
    [Tooltip("Куда «уходит» взгляд вниз (градусы).")]
    [SerializeField] private float pitchDegrees = 25f;
    [Tooltip("Головокружение перед падением.")]
    [SerializeField] private float swayAmplitude = 6f;

    [Header("Затемнение")]
    [SerializeField] private float holdBlackSeconds = 2.5f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звон в ушах / гул — в начале.")]
    [SerializeField] private AudioClip ringClip;
    [Tooltip("Удар о пол — в конце падения.")]
    [SerializeField] private AudioClip thudClip;
    [Tooltip("Плавно приглушить весь звук игры к моменту черноты.")]
    [SerializeField] private bool fadeOutAllAudio = true;

    [Header("После")]
    [Tooltip("Какую сцену загрузить. Пусто — ничего не грузить (только событие ниже).")]
    [SerializeField] private string nextScene = GameFlow.EndScene;
    public UnityEvent onFainted;

    public bool IsFainting { get; private set; }

    private Transform cam;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 posePosition;
    private Quaternion poseRotation;
    private CanvasGroup black;

    private void Awake()
    {
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (controller == null) controller = FindFirstObjectByType<FirstPersonController>();
    }

    /// <summary>Начать потерю сознания.</summary>
    public void Faint()
    {
        if (IsFainting || interactor == null || interactor.PlayerCamera == null) return;
        StartCoroutine(FaintRoutine());
    }

    private void LateUpdate()
    {
        // Ставим камеру каждый кадр после всех: фокус компьютера и покачивание головы не перебьют.
        if (IsFainting && cam != null) cam.SetPositionAndRotation(posePosition, poseRotation);
    }

    private IEnumerator FaintRoutine()
    {
        IsFainting = true;

        interactor.InputEnabled = false;
        if (controller != null) controller.SetControlEnabled(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cam = interactor.PlayerCamera.transform;
        startPosition = posePosition = cam.position;
        startRotation = poseRotation = cam.rotation;
        black = CreateBlackOverlay();

        Play(ringClip);
        float startVolume = AudioListener.volume;

        // 1. Головокружение: покачивание и первые «закрывания век».
        for (float t = 0f; t < 1.4f; t += Time.deltaTime)
        {
            float k = t / 1.4f;
            float sway = Mathf.Sin(t * 5f) * swayAmplitude * k;
            poseRotation = startRotation * Quaternion.Euler(sway * 0.3f, 0f, sway);
            black.alpha = Blink(t, k * 0.6f);
            yield return null;
        }

        // 2. Падение: камера уходит вниз и заваливается набок, веки закрываются.
        Quaternion fallen = startRotation * Quaternion.Euler(pitchDegrees, 0f, rollDegrees);
        Vector3 fallenPosition = startPosition + Vector3.down * dropHeight;
        Quaternion fromRotation = poseRotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, fallSeconds))
        {
            float k = t * t; // медленно в начале, резко в конце — как падение
            posePosition = Vector3.Lerp(startPosition, fallenPosition, k);
            poseRotation = Quaternion.Slerp(fromRotation, fallen, k);
            black.alpha = Mathf.Max(Blink(t * 3f, 0.5f + 0.5f * t), t);
            if (fadeOutAllAudio) AudioListener.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        posePosition = fallenPosition;
        poseRotation = fallen;
        black.alpha = 1f;
        Play(thudClip);

        yield return new WaitForSeconds(holdBlackSeconds);

        onFainted?.Invoke();

        // Громкость — глобальная настройка, её нужно вернуть до смены сцены.
        AudioListener.volume = startVolume;

        if (!string.IsNullOrEmpty(nextScene)) GameFlow.Load(nextScene);
    }

    // Мигание век: открыто/закрыто с нарастающей длительностью закрытия.
    private static float Blink(float time, float strength)
    {
        float wave = Mathf.Clamp01(Mathf.Sin(time * 2.3f) * 0.5f + 0.5f);
        return Mathf.Clamp01(wave * wave * strength + strength * 0.35f);
    }

    private CanvasGroup CreateBlackOverlay()
    {
        var canvasObject = new GameObject("Faint Overlay", typeof(Canvas), typeof(CanvasGroup));
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
