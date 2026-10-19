using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Финальный компьютер в THE_END.
///
/// 1. На экране зашифрованное сообщение — та же мини-игра со сдвигом, что и с картами.
///    Игрок садится за компьютер и двигает ползунок. Когда текст становится нормальным,
///    кнопка EXIT загорается (до этого она видна, но тусклая и не нажимается).
/// 2. Первые нажатия кнопка прыгает по краям экрана, без надписей.
/// 3. Когда кнопку всё-таки нажали — из компьютера к игроку вылезает его рука,
///    крик, мгновенная чернота и титры.
///
/// Вешается на любой объект сцены THE_END (удобно — на монитор).
/// </summary>
// Позже остальных: наша поза камеры (тряска, отдёргивание) перекрывает фокус компьютера.
[DefaultExecutionOrder(1000)]
public class ExitTerminal : MonoBehaviour
{
    [Header("Экран")]
    [SerializeField] private TMP_Text screenText;
    [Tooltip("Ползунок сдвига на мониторе (тот же CipherDial, что в основной сцене).")]
    [SerializeField] private CipherDial cipherDial;
    [Tooltip("Надпись SHIFT: 00 (необязательно).")]
    [SerializeField] private TMP_Text shiftLabel;
    [Tooltip("Кнопка на экране. Подойдёт уже существующий ExtractButton монитора (поменяй ему Prompt на [LMB] EXIT).")]
    [SerializeField] private ExtractButton exitButton;
    [Tooltip("Область, по которой прыгает кнопка (обычно Canvas монитора). Пусто — родитель кнопки.")]
    [SerializeField] private RectTransform buttonArea;

    [Header("Сообщение")]
    [TextArea(4, 12), SerializeField]
    private string message =
        "HAND PATTERN: OPERATOR.\n\n" +
        "YOU NEVER READ THE CARDS.\n" +
        "THE CARDS WERE READING YOU.\n" +
        "NOW WE HAVE ALL OF YOU.\n\n" +
        "PRESS EXIT.";
    [Tooltip("Сдвиг шифра. 0 — случайный.")]
    [SerializeField] private int cipherKey = 0;
    [Tooltip("Пока текст не расшифрован, кнопка видна, но тусклая. Выключи — будет совсем спрятана.")]
    [SerializeField] private bool showDimmedButton = true;

    [Header("Прыгающая кнопка")]
    [Tooltip("Сколько раз кнопка отпрыгнет, прежде чем нажаться.")]
    [SerializeField] private int dodges = 11;
    [Tooltip("Время одного прыжка (сек).")]
    [SerializeField] private float jumpSeconds = 0.12f;
    [Tooltip("Отступ от краёв экрана, чтобы кнопка не уезжала за край.")]
    [SerializeField] private float edgeMargin = 20f;

    [Header("Рука из компьютера")]
    [Tooltip("Рука, спрятанная внутри монитора (выключенная). Вылезет к игроку.")]
    [SerializeField] private Transform handOut;
    [Tooltip("Вкл — рука летит, не поворачиваясь: как ты повернул её в сцене (ладонью к игроку), так и прилетит.\n" +
             "Выкл — в конце повернётся на Hand Out Rotation относительно взгляда игрока.")]
    [SerializeField] private bool keepSceneRotation = true;
    [Tooltip("Используется, только если Keep Scene Rotation выключен.")]
    [SerializeField] private Vector3 handOutRotation = new Vector3(0f, 180f, 0f);
    [SerializeField] private float handSeconds = 1.6f;
    [Tooltip("На каком расстоянии от лица игрока окажется центр руки.")]
    [SerializeField] private float handStopDistance = 0.25f;
    [TextArea, SerializeField] private string goodbyeText = "GOODBYE, OPERATOR.";

    [Header("Тряска (после первого нажатия EXIT)")]
    [Tooltip("Сила тряски в градусах — как при потере сознания.")]
    [SerializeField] private float shakeDegrees = 7f;
    [Tooltip("За сколько секунд тряска набирает силу.")]
    [SerializeField] private float shakeRampSeconds = 1.5f;
    [Tooltip("Сколько секунд после GOODBYE, OPERATOR тряска затихает.")]
    [SerializeField] private float shakeStopSeconds = 0.25f;

    [Header("Испуг оператора")]
    [Tooltip("Когда (доля полёта руки, 0..1) оператор отшатывается.")]
    [SerializeField, Range(0.5f, 1f)] private float flinchAt = 0.85f;
    [Tooltip("Насколько камера отпрыгивает назад (метры).")]
    [SerializeField] private float flinchDistance = 0.15f;
    [Tooltip("Насколько голова откидывается вверх (градусы).")]
    [SerializeField] private float flinchPitch = 8f;
    [SerializeField] private float flinchSeconds = 0.18f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip grantedClip;
    [SerializeField] private AudioSource errorAudio, backAudio, tensionMusic, breathingAudio, heartbeatAudio, screamAudio;
    [SerializeField] private EndCreditsRoll credits;
    public int PressCount {get;private set;}
    public int RequiredPresses=>dodges+1;
    public bool HasBlackedOut {get;private set;}
    private CanvasGroup endOverlay;
    private bool wasFocused;
    private Camera blackCamera;
    private int savedCullingMask;
    private CameraClearFlags savedClearFlags;
    private Color savedCameraColour;
    private bool screamTriggered;

    private enum State { Decoding, ButtonActive, Ending }

    private State state = State.Decoding;
    private PlayerInteractor interactor;
    private string cipherText;
    private int key;
    private int dodgesDone;
    private int lastEdge = -1;
    private Coroutine jumpRoutine;

    // Камера: базовая поза (за компьютером) + тряска + отдёргивание.
    private Transform cam;
    private bool cameraOverride;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float shakeAmount;   // 0..1
    private float flinch;        // 0..1
    private bool shaking;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        interactor = FindFirstObjectByType<PlayerInteractor>();

        // Обычная логика монитора здесь не нужна — экраном управляет терминал.
        foreach (var display in FindObjectsByType<MonitorDisplay>(FindObjectsSortMode.None))
            display.enabled = false;

        if (buttonArea == null && exitButton != null) buttonArea = exitButton.transform.parent as RectTransform;
        if (handOut != null) handOut.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (exitButton != null) exitButton.Pressed += OnExitPressed;
        if (cipherDial != null) cipherDial.ValueChanged += OnDialChanged;
    }

    private void OnDisable()
    {
        if (exitButton != null) exitButton.Pressed -= OnExitPressed;
        if (cipherDial != null) cipherDial.ValueChanged -= OnDialChanged;
        StopAllCoroutines();
        foreach(var source in new[]{audioSource,errorAudio,backAudio,tensionMusic,breathingAudio,heartbeatAudio,screamAudio})if(source)source.Stop();
        if(endOverlay)Destroy(endOverlay.gameObject);
        if(blackCamera){blackCamera.cullingMask=savedCullingMask;blackCamera.clearFlags=savedClearFlags;blackCamera.backgroundColor=savedCameraColour;}
    }

    private void Start()
    {
        if (screenText != null)
        {
            // Текст всегда влезает в экран.
            screenText.enableAutoSizing = true;
            screenText.fontSizeMax = 26f;
            screenText.fontSizeMin = 10f;
            screenText.textWrappingMode = TextWrappingModes.Normal;
            screenText.overflowMode = TextOverflowModes.Truncate;
        }

        if (cipherDial == null)
        {
            Debug.LogError("ExitTerminal: не задан Cipher Dial — кнопка EXIT сразу активна.", this);
            Unlock();
            return;
        }

        int min = Mathf.Max(1, cipherDial.MinValue + 1);
        key = cipherKey > 0 ? cipherKey : Random.Range(min, cipherDial.MaxValue + 1);
        cipherText = CaesarCipher.Shift(message, key);

        cipherDial.ResetValue();
        cipherDial.SetActive(true);
        SetButtonLocked();
        Redraw();
    }

    private void SetButtonLocked()
    {
        if (exitButton == null) return;
        if (showDimmedButton) exitButton.SetDimmed(true);
        else exitButton.SetVisible(false);
    }

    private void OnDialChanged(int value)
    {
        if (state != State.Decoding) return;
        Redraw();
        if (value == key) Unlock();
    }

    private void Redraw()
    {
        if (screenText != null)
        {
            screenText.text = cipherText != null && cipherDial != null
                ? CaesarCipher.Shift(cipherText, -cipherDial.Value)
                : message;
            screenText.maxVisibleCharacters = int.MaxValue;
        }
        if (shiftLabel != null && cipherDial != null)
            shiftLabel.text = string.Format("SHIFT: {0:00}", cipherDial.Value);
    }

    private void Unlock()
    {
        state = State.ButtonActive;
        if (screenText != null) { screenText.text = message; screenText.maxVisibleCharacters = int.MaxValue; }
        if (cipherDial != null) cipherDial.SetActive(false); // текст больше не сбить
        if (exitButton != null) { exitButton.SetDimmed(false); exitButton.SetVisible(true); }
        Play(grantedClip);
    }

    // ---------- Камера ----------

    private void LateUpdate()
    {
        bool focused=interactor&&interactor.IsFocused;
        if(wasFocused&&!focused&&state!=State.Ending&&backAudio&&backAudio.clip)backAudio.PlayOneShot(backAudio.clip);
        wasFocused=focused;
        if(HasBlackedOut)return;
        if(shaking&&state==State.ButtonActive)
        {
            float level=Mathf.Clamp01((PressCount-1)/(float)Mathf.Max(1,dodges));
            shakeAmount=Mathf.MoveTowards(shakeAmount,Mathf.Lerp(.15f,1,level),Time.unscaledDeltaTime*.6f);
            Raise(tensionMusic,Mathf.Lerp(.055f,.65f,level));Raise(breathingAudio,Mathf.Lerp(.08f,.46f,level));Raise(heartbeatAudio,Mathf.Lerp(.12f,.5f,level));
            if(heartbeatAudio)heartbeatAudio.pitch=Mathf.Lerp(.9f,1.75f,level);
        }
        if (!cameraOverride || cam == null) return;

        // Игрок встал из-за компьютера (Esc) — камеру не держим, тряска продолжится, когда сядет снова.
        if (state != State.Ending && interactor != null && !interactor.IsFocused) return;
        if (state != State.Ending) UpdateBasePose();

        float t = Time.time * 18f;
        Quaternion shake = Quaternion.Euler(
            (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 2f * shakeDegrees * shakeAmount,
            (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 2f * shakeDegrees * 0.6f * shakeAmount,
            (Mathf.PerlinNoise(t, 0.7f) - 0.5f) * 2f * shakeDegrees * 1.3f * shakeAmount);

        Vector3 position = basePosition - baseRotation * Vector3.forward * (flinchDistance * flinch);
        Quaternion rotation = baseRotation * Quaternion.Euler(-flinchPitch * flinch, 0f, 0f) * shake;
        cam.SetPositionAndRotation(position, rotation);
    }

    private void TakeCamera()
    {
        if (cameraOverride || interactor == null || interactor.PlayerCamera == null) return;
        cam = interactor.PlayerCamera.transform;
        basePosition = cam.position;
        baseRotation = cam.rotation;
        UpdateBasePose();
        cameraOverride = true;
    }

    // Спокойная поза — точка фокуса компьютера (а не текущая, уже трясущаяся камера).
    private void UpdateBasePose()
    {
        if (interactor == null || interactor.CurrentFocus == null) return;
        Transform point = interactor.CurrentFocus.FocusPoint;
        basePosition = point.position;
        baseRotation = point.rotation;
    }

    // Первое нажатие EXIT: текст пропадает, начинается тряска.
    private void StartShaking()
    {
        if (shaking) return;
        shaking = true;
        if (screenText != null) screenText.text = "";
        TakeCamera();
        shakeRoutine = StartCoroutine(Ramp(v => shakeAmount = v, 0f, .15f, shakeRampSeconds, false));
        BeginLoop(tensionMusic,.055f);BeginLoop(breathingAudio,.08f);BeginLoop(heartbeatAudio,.12f);
    }

    private IEnumerator Ramp(System.Action<float> set, float from, float to, float seconds, bool easeOut)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
        {
            float k = easeOut ? 1f - (1f - t) * (1f - t) : t;
            set(Mathf.Lerp(from, to, k));
            yield return null;
        }
        set(to);
    }

    // ---------- Кнопка EXIT ----------

    private void OnExitPressed()
    {
        if (state != State.ButtonActive) return;
        PressCount++;
        if(errorAudio&&errorAudio.clip){errorAudio.Stop();errorAudio.Play();}
        StartShaking();

        if (dodgesDone < dodges)
        {
            dodgesDone++;
            if (screenText != null) screenText.text = "";
            Jump();
            return;
        }

        state = State.Ending;
        exitButton.SetVisible(false);
        StartCoroutine(Ending());
    }

    // Кнопка прыгает в один из углов или середин краёв экрана, каждый раз в другой.
    private void Jump()
    {
        var rect = exitButton.transform as RectTransform;
        if (rect == null || buttonArea == null) return;

        Rect area = buttonArea.rect;
        Vector2 size = rect.rect.size;
        float x = Mathf.Max(0f, area.width / 2f - size.x / 2f - edgeMargin);
        float y = Mathf.Max(0f, area.height / 2f - size.y / 2f - edgeMargin);
        Vector2 offset = area.center; // если pivot области не по центру

        Vector2[] edges =
        {
            new Vector2(-x,  y), new Vector2(0f,  y), new Vector2(x,  y),
            new Vector2( x, 0f),
            new Vector2( x, -y), new Vector2(0f, -y), new Vector2(-x, -y),
            new Vector2(-x, 0f),
        };

        // Подальше от текущего места, чтобы курсор точно оказался не над кнопкой.
        Vector2 current = rect.anchoredPosition;
        int pick = -1;
        float best = -1f;
        for (int tries = 0; tries < 12; tries++)
        {
            int i = Random.Range(0, edges.Length);
            if (i == lastEdge) continue;
            float d = Vector2.Distance(current, edges[i] + offset);
            if (d > best) { best = d; pick = i; }
            if (d > area.width * 0.45f) break;
        }
        if (pick < 0) pick = (lastEdge + 4) % edges.Length;
        lastEdge = pick;

        if (jumpRoutine != null) StopCoroutine(jumpRoutine);
        jumpRoutine = StartCoroutine(JumpTo(rect, edges[pick] + offset));
    }

    private IEnumerator JumpTo(RectTransform rect, Vector2 target)
    {
        Vector2 from = rect.anchoredPosition;
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, jumpSeconds))
        {
            rect.anchoredPosition = Vector2.Lerp(from, target, 1f - (1f - t) * (1f - t));
            yield return null;
        }
        rect.anchoredPosition = target;
        jumpRoutine = null;
    }

    // ---------- Финал ----------

    private IEnumerator Ending()
    {
        if (interactor != null)
        {
            interactor.InputEnabled=false;interactor.StopAllCoroutines();
            if(interactor.Controller){interactor.Controller.SetControlEnabled(false);interactor.Controller.enabled=false;}
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        TakeCamera();

        // Ещё немного трясёт, потом GOODBYE, OPERATOR — и тряска разом стихает.
        yield return new WaitForSeconds(0.6f);
        if (screenText != null) screenText.text = goodbyeText;
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        yield return Ramp(v => shakeAmount = v, shakeAmount, 0f, shakeStopSeconds, true);

        yield return new WaitForSeconds(1.2f);

        // Резкий выпад из экрана, затем мгновенная чернота.
        // Конечная поза считается один раз — так движение ровное, без дёрганья.
        if (handOut != null && interactor != null && interactor.PlayerCamera != null)
        {
            handOut.gameObject.SetActive(true);
            PlayScream();
            if (handOut.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
            foreach (var c in handOut.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var p in handOut.GetComponentsInChildren<Pickupable>()) p.enabled = false;

            Vector3 center = FinalHand.ModelCenter(handOut);
            Vector3 scaledCenter = Vector3.Scale(handOut.lossyScale, center);

            // Двигаем центр модели, а не точку опоры — тогда поворот не «размахивает» рукой.
            Vector3 fromCenter = handOut.position + handOut.rotation * scaledCenter;
            Quaternion fromRotation = handOut.rotation;
            // Цель — от спокойной позы камеры (до тряски и испуга).
            Quaternion toRotation = keepSceneRotation ? fromRotation : baseRotation * Quaternion.Euler(handOutRotation);
            Vector3 toCenter = basePosition + baseRotation * Vector3.forward * handStopDistance;
            bool flinched = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, handSeconds))
            {
                float k = 1f-Mathf.Pow(1f-t,3f);
                float turn = Mathf.SmoothStep(0f, 1f, t); // поворот плавнее, заранее
                Quaternion rotation = Quaternion.Slerp(fromRotation, toRotation, turn);
                Vector3 centerNow = Vector3.Lerp(fromCenter, toCenter, k);
                handOut.SetPositionAndRotation(centerNow - rotation * scaledCenter, rotation);

                // Рука почти у лица — оператор резко отшатывается.
                if (!flinched && t >= flinchAt)
                {
                    flinched = true;
                    StartCoroutine(Ramp(v => flinch = v, 0f, 1f, flinchSeconds, true));
                }
                if(t>=.85f)break;
                yield return null;
            }
        }

        HasBlackedOut=true;endOverlay=CreateEndOverlay();endOverlay.alpha=1;
        foreach(var root in gameObject.scene.GetRootGameObjects())
        {
            foreach(var printer in root.GetComponentsInChildren<PhotoPrinterSpawner>(true))printer.enabled=false;
            foreach(var spawner in root.GetComponentsInChildren<CassetteTubeSpawner>(true))spawner.enabled=false;
            foreach(var source in root.GetComponentsInChildren<AudioSource>(true))if(source!=screamAudio)source.Stop();
        }
        if(interactor&&interactor.PlayerCamera)
        {
            blackCamera=interactor.PlayerCamera;savedCullingMask=blackCamera.cullingMask;savedClearFlags=blackCamera.clearFlags;savedCameraColour=blackCamera.backgroundColor;
            blackCamera.cullingMask=0;blackCamera.clearFlags=CameraClearFlags.SolidColor;blackCamera.backgroundColor=Color.black;
        }
        if(!screamTriggered)PlayScream();
        yield return null;
        while(screamAudio&&screamAudio.isPlaying)yield return null;
        yield return new WaitForSecondsRealtime(.45f);
        if(credits)credits.Play();
        else Debug.LogError("THE_END is missing its credits roll.",this);
    }

    private CanvasGroup CreateEndOverlay()
    {
        var canvasObject = new GameObject("End Overlay", typeof(Canvas), typeof(CanvasGroup));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        var background = new GameObject("Black", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = Color.black;

        return canvasObject.GetComponent<CanvasGroup>();
    }
    private static void BeginLoop(AudioSource source,float volume)
    {if(source&&source.clip){source.volume=volume;source.loop=true;source.Play();}}
    private static void Raise(AudioSource source,float volume)
    {if(source)source.volume=Mathf.MoveTowards(source.volume,volume,Time.unscaledDeltaTime*.5f);}
    private void PlayScream()
    {screamTriggered=true;if(screamAudio&&screamAudio.clip){screamAudio.loop=false;screamAudio.Play();}}

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
