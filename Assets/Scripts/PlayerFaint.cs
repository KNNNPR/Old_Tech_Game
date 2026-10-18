using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Потеря сознания и финальные затемнения.
///
/// BeginWarning() — начало: красное мерцание в комнате, камера трясётся всё сильнее (управление ещё есть).
/// Faint()        — обморок: резкие рывки головы, падение набок, веки, чернота → следующий день.
/// BlackoutAndLoad(scene) — красная тревога, тряска и чернота → указанная сцена (финал с рукой).
///
/// Вешается на любой объект основной сцены (удобно — на Player). Затемнение и красный свет создаёт сам.
/// </summary>
// Позже остальных скриптов: наша поза камеры должна перекрыть фокус компьютера и покачивание головы.
[DefaultExecutionOrder(1000)]
public class PlayerFaint : MonoBehaviour
{
    public enum AfterFaint { NextDay, LoadScene }

    [Header("Ссылки (если пусто — найдутся сами)")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;

    [Header("Тряска перед падением")]
    [Tooltip("Максимальная тряска камеры, пока экран распадается (градусы).")]
    [SerializeField] private float warningShake = 7f;
    [Tooltip("Рывки головы в начале обморока (градусы).")]
    [SerializeField] private float lurchDegrees = 22f;
    [SerializeField] private float lurchSeconds = 1.6f;

    [Header("Падение")]
    [SerializeField] private float fallSeconds = 1.8f;
    [Tooltip("Насколько опускается камера (метры).")]
    [SerializeField] private float dropHeight = 1.3f;
    [Tooltip("Завал головы вбок (градусы).")]
    [SerializeField] private float rollDegrees = 80f;
    [Tooltip("Куда «уходит» взгляд вниз (градусы).")]
    [SerializeField] private float pitchDegrees = 25f;
    [SerializeField] private float holdBlackSeconds = 2.5f;

    [Header("Красное мерцание")]
    [Tooltip("Свет, который будет мигать красным. Пусто — над игроком создастся свой красный свет.")]
    [SerializeField] private Light[] alarmLights;
    [SerializeField] private Color alarmColor = new Color(1f, 0.05f, 0.03f);
    [SerializeField] private float alarmIntensity = 4f;
    [SerializeField] private float alarmRange = 12f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звон в ушах / гул — когда начинается тряска.")]
    [SerializeField] private AudioClip ringClip;
    [Tooltip("Удар о пол — в конце падения.")]
    [SerializeField] private AudioClip thudClip;
    [Tooltip("Плавно приглушить весь звук игры к моменту черноты.")]
    [SerializeField] private bool fadeOutAllAudio = true;

    [Header("После обморока")]
    [Tooltip("NextDay — очнуться в следующем дне. LoadScene — загрузить сцену из поля ниже.")]
    [SerializeField] private AfterFaint afterFaint = AfterFaint.NextDay;
    [SerializeField] private string sceneAfterFaint = GameFlow.EndScene;
    public UnityEvent onFainted;

    public bool IsWarning { get; private set; }
    public bool IsFainting { get; private set; }

    private Transform cam;
    private Quaternion restLocalRotation;
    private float shakeAmount;               // 0..1 — сила тряски в фазе предупреждения
    private Vector3 posePosition;            // абсолютная поза камеры во время падения
    private Quaternion poseRotation;
    private CanvasGroup black;
    private bool alarmRunning;

    private void Awake()
    {
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (controller == null) controller = FindFirstObjectByType<FirstPersonController>();
        if (interactor != null && interactor.PlayerCamera != null)
        {
            cam = interactor.PlayerCamera.transform;
            restLocalRotation = cam.localRotation;
        }
    }

    // ---------- Публичные команды ----------

    /// <summary>Начало приступа: красное мерцание и нарастающая тряска. Управление ещё есть.</summary>
    public void BeginWarning(float rampSeconds = 3f)
    {
        if (IsWarning || IsFainting || cam == null) return;
        IsWarning = true;
        Play(ringClip);
        StartAlarm();
        StartCoroutine(RampShake(rampSeconds));
    }

    /// <summary>Обморок и переход к следующему дню (или сцене из настроек).</summary>
    public void Faint()
    {
        if (IsFainting || cam == null) return;
        StartCoroutine(FaintRoutine(afterFaint == AfterFaint.NextDay ? null : sceneAfterFaint));
    }

    /// <summary>Финал: тревога, тряска, чернота и загрузка сцены.</summary>
    public void BlackoutAndLoad(string scene, float shakeSeconds = 2.5f)
    {
        if (IsFainting || cam == null) return;
        StartCoroutine(BlackoutRoutine(scene, shakeSeconds));
    }

    // ---------- Камера ----------

    private void LateUpdate()
    {
        if (cam == null) return;

        if (IsFainting)
        {
            cam.SetPositionAndRotation(posePosition, poseRotation);
            return;
        }

        if (IsWarning && shakeAmount > 0f)
        {
            // Резкая тряска: шум с высокой частотой, сила растёт.
            float t = Time.time * 18f;
            Quaternion shake = Quaternion.Euler(
                (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * warningShake * shakeAmount,
                (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f * warningShake * 0.6f * shakeAmount,
                (Mathf.PerlinNoise(t, 0.7f) - 0.5f) * 2f * warningShake * 1.3f * shakeAmount);

            // За компьютером поза камеры задаётся заново каждый кадр — добавляем тряску сверху.
            // В обычном режиме локальный поворот камеры никто не трогает — задаём его от исходного.
            if (interactor != null && interactor.IsFocused) cam.rotation *= shake;
            else cam.localRotation = restLocalRotation * shake;
        }
    }

    private IEnumerator RampShake(float seconds)
    {
        for (float t = 0f; t < 1f && !IsFainting; t += Time.deltaTime / Mathf.Max(0.1f, seconds))
        {
            shakeAmount = t;
            yield return null;
        }
        shakeAmount = 1f;
    }

    // ---------- Обморок ----------

    private IEnumerator FaintRoutine(string sceneToLoad)
    {
        if (!IsWarning) { Play(ringClip); StartAlarm(); }
        TakeControl();

        Vector3 startPosition = posePosition = cam.position;
        Quaternion startRotation = poseRotation = cam.rotation;
        float startVolume = AudioListener.volume;

        // 1. Резкие рывки головы: каждые 0.15–0.35 с новая случайная цель, камера к ней «дёргается».
        Quaternion lurchTarget = startRotation;
        float nextLurch = 0f;
        for (float t = 0f; t < lurchSeconds; t += Time.deltaTime)
        {
            float k = t / lurchSeconds;
            if (t >= nextLurch)
            {
                nextLurch = t + Random.Range(0.15f, 0.35f);
                lurchTarget = startRotation * Quaternion.Euler(
                    Random.Range(-0.5f, 1f) * lurchDegrees,
                    Random.Range(-0.5f, 0.5f) * lurchDegrees,
                    Random.Range(-1f, 1f) * lurchDegrees * (0.5f + k));
            }
            poseRotation = Quaternion.Slerp(poseRotation, lurchTarget, 18f * Time.deltaTime);
            posePosition = startPosition + Vector3.down * (0.15f * k);
            black.alpha = Blink(t * 2f, 0.25f + 0.45f * k);
            yield return null;
        }

        // 2. Падение: камера уходит вниз и заваливается набок, веки закрываются.
        Vector3 fromPosition = posePosition;
        Quaternion fromRotation = poseRotation;
        Quaternion fallen = startRotation * Quaternion.Euler(pitchDegrees, 0f, rollDegrees);
        Vector3 fallenPosition = startPosition + Vector3.down * dropHeight;

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, fallSeconds))
        {
            float k = t * t; // медленно в начале, резко в конце — как падение
            posePosition = Vector3.Lerp(fromPosition, fallenPosition, k);
            poseRotation = Quaternion.Slerp(fromRotation, fallen, k);
            black.alpha = Mathf.Max(Blink(t * 3f, 0.6f + 0.4f * t), t);
            if (fadeOutAllAudio) AudioListener.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        posePosition = fallenPosition;
        poseRotation = fallen;
        black.alpha = 1f;
        StopAlarm();
        Play(thudClip);

        yield return new WaitForSeconds(holdBlackSeconds);

        onFainted?.Invoke();
        AudioListener.volume = startVolume; // громкость глобальная — вернуть до смены сцены

        if (string.IsNullOrEmpty(sceneToLoad)) GameFlow.WakeUpNextDay();
        else GameFlow.Load(sceneToLoad);
    }

    // ---------- Финал ----------

    private IEnumerator BlackoutRoutine(string scene, float shakeSeconds)
    {
        if (!IsWarning) BeginWarning(shakeSeconds * 0.5f);
        interactor.InputEnabled = false;
        if (black == null) black = CreateBlackOverlay();
        float startVolume = AudioListener.volume;

        yield return new WaitForSeconds(shakeSeconds);

        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
        {
            black.alpha = t;
            if (fadeOutAllAudio) AudioListener.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        black.alpha = 1f;
        StopAlarm();

        yield return new WaitForSeconds(1.5f);
        AudioListener.volume = startVolume;
        GameFlow.Load(scene);
    }

    // ---------- Красное мерцание ----------

    private void StartAlarm()
    {
        if (alarmRunning) return;
        alarmRunning = true;

        if (alarmLights == null || alarmLights.Length == 0)
        {
            // Свой красный свет над игроком, чтобы мигала вся комната вокруг.
            var lightObject = new GameObject("Faint Alarm Light");
            lightObject.transform.SetParent(cam, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.8f, 0.5f);
            var alarm = lightObject.AddComponent<Light>();
            alarm.type = LightType.Point;
            alarm.range = alarmRange;
            alarm.shadows = LightShadows.None;
            alarmLights = new[] { alarm };
        }

        foreach (Light alarm in alarmLights)
            if (alarm != null) { alarm.color = alarmColor; alarm.enabled = true; }

        StartCoroutine(AlarmRoutine());
    }

    private IEnumerator AlarmRoutine()
    {
        while (alarmRunning)
        {
            // Неровное мерцание: вспышка, короткое затухание, иногда двойной всплеск.
            float intensity = Random.value < 0.3f ? 0f : alarmIntensity * Random.Range(0.5f, 1.2f);
            foreach (Light alarm in alarmLights)
                if (alarm != null) alarm.intensity = intensity;
            yield return new WaitForSeconds(Random.Range(0.05f, 0.22f));
        }
        foreach (Light alarm in alarmLights)
            if (alarm != null) alarm.intensity = 0f;
    }

    private void StopAlarm() => alarmRunning = false;

    // ---------- Служебное ----------

    private void TakeControl()
    {
        IsFainting = true;
        IsWarning = false;
        interactor.InputEnabled = false;
        if (controller != null) controller.SetControlEnabled(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (black == null) black = CreateBlackOverlay();
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