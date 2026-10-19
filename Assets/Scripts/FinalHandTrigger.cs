using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Финал дня 4. Игрок вставляет руку (предмет с FinalHand) в слот компьютера:
/// монитор «считывает» руку, затем камера рывками переключается
/// экран → рука → экран → рука → экран вплотную и влетает внутрь экрана.
/// После этого — следующий шаг сюжета (THE_END).
/// Вешается на любой объект основной сцены.
/// </summary>
// Позже остальных скриптов: наша поза камеры перекрывает игрока, фокус компьютера и покачивание головы.
[DefaultExecutionOrder(1000)]
public class FinalHandTrigger : MonoBehaviour
{
    [Tooltip("Слот компьютера, куда вставляются карты.")]
    [SerializeField] private ItemSlot computerSlot;

    [Header("Кадры")]
    [Tooltip("Точка в центре экрана монитора. Камера летит в неё.")]
    [SerializeField] private Transform screenTarget;
    [Tooltip("Монитор — напечатать «считывание». Если пусто — найдётся сам.")]
    [SerializeField] private MonitorDisplay monitor;
    [TextArea, SerializeField] private string readingText = "HAND DETECTED.\nREADING...";
    [TextArea, SerializeField] private string matchText = "PATTERN MATCH: OPERATOR\nWELCOME HOME.";
    [Tooltip("Сколько секунд идёт «считывание» до первого кадра.")]
    [SerializeField] private float readingSeconds = 3f;

    [Tooltip("Расстояние до цели в каждом кадре: экран, рука, экран, рука, экран (вплотную).")]
    [SerializeField] private float[] shotDistances = { 0.9f, 0.55f, 0.45f, 0.3f, 0.16f };
    [Tooltip("Длительность каждого кадра. Кадры становятся всё короче — нарастает ритм.")]
    [SerializeField] private float[] shotSeconds = { 1.3f, 0.9f, 0.7f, 0.5f, 0.9f };
    [Tooltip("Насколько камера «наезжает» за время кадра (доля расстояния).")]
    [SerializeField, Range(0f, 0.8f)] private float pushIn = 0.25f;
    [SerializeField] private float startFov = 55f;
    [SerializeField] private float endFov = 30f;
    [SerializeField] private float flyInSeconds = 0.9f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Когда компьютер принимает руку.")]
    [SerializeField] private AudioClip acceptClip;
    [Tooltip("Удар на каждой смене кадра.")]
    [SerializeField] private AudioClip cutClip;
    [Tooltip("Влёт в экран.")]
    [SerializeField] private AudioClip flyInClip;

    public UnityEvent onHandInserted;

    private bool triggered;
    private bool cameraLocked;
    private Transform cam;
    private Camera camComponent;
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
        StartCoroutine(FinalRoutine(item.transform));
    }

    private IEnumerator FinalRoutine(Transform hand)
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

        // 1. Считывание.
        if (monitor != null) monitor.ShowSystemText(readingText);
        yield return new WaitForSeconds(readingSeconds * 0.6f);
        if (monitor != null) monitor.ShowSystemText(matchText);
        yield return new WaitForSeconds(readingSeconds * 0.4f);

        if (interactor == null || interactor.PlayerCamera == null || screenTarget == null)
        {
            Debug.LogError("FinalHandTrigger: нет камеры игрока или не задан Screen Target — пропускаю пролёт.", this);
            GameFlow.Advance();
            yield break;
        }

        cam = interactor.PlayerCamera.transform;
        camComponent = interactor.PlayerCamera;
        black = CreateBlackOverlay();
        cameraLocked = true;

        // Направления взгляда считаем от того места, где стоял игрок, — кадры будут «с его стороны».
        Vector3 viewer = cam.position;
        Vector3 screenDir = (screenTarget.position - viewer).normalized;
        Vector3 handPoint = HandCenter(hand);
        Vector3 handDir = (handPoint - viewer).normalized;

        // 2. Рывки: экран, рука, экран, рука, экран вплотную.
        int shots = Mathf.Min(shotDistances.Length, shotSeconds.Length);
        for (int i = 0; i < shots; i++)
        {
            bool onScreen = i % 2 == 0;
            Vector3 target = onScreen ? screenTarget.position : HandCenter(hand);
            Vector3 dir = onScreen ? screenDir : handDir;

            // Каждый кадр чуть с другого угла — смотрится как резкая нервная склейка.
            float side = (i % 3 - 1) * 10f;
            dir = Quaternion.AngleAxis(side, Vector3.up) * dir;

            float fov = Mathf.Lerp(startFov, endFov, shots > 1 ? i / (float)(shots - 1) : 1f);
            yield return Blink();
            Play(cutClip);
            yield return Shot(target, dir, shotDistances[i], shotSeconds[i], fov);
        }

        // 3. Влёт в экран: камера проходит сквозь стекло, мир уходит в черноту.
        Play(flyInClip);
        float startDistance = shotDistances.Length > 0 ? shotDistances[shotDistances.Length - 1] * (1f - pushIn) : 0.15f;
        Vector3 flyDir = screenDir;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, flyInSeconds))
        {
            float k = t * t;
            float distance = Mathf.Lerp(startDistance, -0.35f, k);
            posePosition = screenTarget.position - flyDir * distance;
            poseRotation = Quaternion.LookRotation(flyDir, Vector3.up) * Quaternion.Euler(0f, 0f, k * 25f);
            camComponent.fieldOfView = Mathf.Lerp(endFov, 100f, k);
            black.alpha = Mathf.Clamp01((t - 0.55f) / 0.35f);
            yield return null;
        }
        black.alpha = 1f;

        yield return new WaitForSeconds(1f);
        GameFlow.Advance();
    }

    private IEnumerator Shot(Vector3 target, Vector3 dir, float distance, float seconds, float fov)
    {
        camComponent.fieldOfView = fov;
        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);

        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
        {
            float d = distance * (1f - pushIn * Mathf.SmoothStep(0f, 1f, t));
            // Лёгкая дрожь «ручной камеры».
            Quaternion shake = Quaternion.Euler(
                (Mathf.PerlinNoise(Time.time * 9f, 0.2f) - 0.5f) * 2.5f,
                (Mathf.PerlinNoise(0.6f, Time.time * 9f) - 0.5f) * 2.5f, 0f);

            posePosition = target - dir * d;
            poseRotation = look * shake;
            yield return null;
        }
    }

    // Короткая чёрная вспышка на склейке.
    private IEnumerator Blink()
    {
        black.alpha = 1f;
        yield return new WaitForSeconds(0.06f);
        black.alpha = 0f;
    }

    private static Vector3 HandCenter(Transform hand)
    {
        var renderers = hand.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return hand.position;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds.center;
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