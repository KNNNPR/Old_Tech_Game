using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Финальный компьютер в THE_END.
///
/// 1. На экране: «рука считана» и загадка. Игрок садится за компьютер (FocusStation)
///    и набирает ответ на клавиатуре, Enter — проверить. Ответ по умолчанию — EXIT
///    (первые буквы строк загадки).
/// 2. Появляется кнопка EXIT. Первые нажатия она «убегает» в другое место экрана.
/// 3. Когда кнопку всё-таки нажали — из компьютера к игроку вылезает его рука,
///    чернота, надпись и возврат в меню.
///
/// Вешается на любой объект сцены THE_END (удобно — на монитор).
/// </summary>
public class ExitTerminal : MonoBehaviour
{
    [Header("Экран")]
    [SerializeField] private TMP_Text screenText;
    [Tooltip("Кнопка на экране. Подойдёт уже существующий ExtractButton монитора (поменяй ему Prompt на [LMB] EXIT).")]
    [SerializeField] private ExtractButton exitButton;
    [Tooltip("Область, по которой бегает кнопка (обычно Canvas монитора). Пусто — родитель кнопки.")]
    [SerializeField] private RectTransform buttonArea;

    [Header("Загадка")]
    [TextArea(2, 4), SerializeField] private string header = "HAND PATTERN: OPERATOR. ACCEPTED.";
    [TextArea(4, 12), SerializeField] private string puzzle =
        "EVERY CARD YOU BURNED CAME BACK.\n" +
        "X-RAYS OF YOUR HAND ARE ON FILE.\n" +
        "IT KNOWS YOUR SHIFT BY HEART.\n" +
        "THE ONLY WAY OUT IS A WORD.";
    [SerializeField] private string prompt = "TYPE THE WORD:";
    [SerializeField] private string answer = "EXIT";
    [SerializeField] private string wrongText = "ACCESS DENIED.";
    [Tooltip("Подсказка появляется после стольких ошибок.")]
    [SerializeField] private int hintAfterMistakes = 2;
    [SerializeField] private string hint = "READ THE FIRST LETTERS.";
    [SerializeField] private string solvedText = "ACCESS GRANTED.\nPRESS EXIT TO LEAVE.";
    [SerializeField] private int maxInputLength = 12;

    [Header("Убегающая кнопка")]
    [Tooltip("Сколько раз кнопка увернётся, прежде чем нажаться.")]
    [SerializeField] private int dodges = 2;
    [SerializeField] private string[] dodgeTaunts = { "NO.", "NOT YET." };
    [Tooltip("Отступ от краёв экрана, чтобы кнопка не уезжала за край.")]
    [SerializeField] private float edgeMargin = 70f;

    [Header("Рука из компьютера")]
    [Tooltip("Рука, спрятанная внутри монитора (выключенная). Вылезет к игроку.")]
    [SerializeField] private Transform handOut;
    [SerializeField] private float handSeconds = 2.2f;
    [Tooltip("На каком расстоянии от лица игрока рука остановится.")]
    [SerializeField] private float handStopDistance = 0.2f;
    [TextArea, SerializeField] private string goodbyeText = "GOODBYE, OPERATOR.";

    [Header("Конец")]
    [TextArea, SerializeField] private string endTitle = "THE END";
    [SerializeField] private float endHoldSeconds = 5f;
    [Tooltip("Вернуться в меню после надписи.")]
    [SerializeField] private bool returnToMenu = true;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip keyClip;
    [SerializeField] private AudioClip deniedClip;
    [SerializeField] private AudioClip grantedClip;
    [SerializeField] private AudioClip dodgeClip;
    [SerializeField] private AudioClip handClip;

    private enum State { Typing, Checking, ButtonShown, Ending }

    private State state = State.Typing;
    private PlayerInteractor interactor;
    private readonly StringBuilder input = new StringBuilder();
    private string statusLine = "";
    private int mistakes;
    private int dodgesDone;

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
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        if (exitButton != null) exitButton.Pressed += OnExitPressed;
    }

    private void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        if (exitButton != null) exitButton.Pressed -= OnExitPressed;
    }

    private void Start()
    {
        if (exitButton != null) exitButton.SetVisible(false);
        Redraw();
    }

    private void Update()
    {
        if (state != State.Typing) return;

        // Мигающий курсор ввода.
        if (Time.frameCount % 15 == 0) Redraw();

        Keyboard kb = Keyboard.current;
        if (kb == null || !CanType()) return;

        if (kb.backspaceKey.wasPressedThisFrame && input.Length > 0)
        {
            input.Length--;
            Play(keyClip);
            Redraw();
        }
        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            StartCoroutine(Check());
        }
    }

    // Печатать можно только сидя за компьютером.
    private bool CanType() => interactor == null || interactor.IsFocused;

    private void OnTextInput(char c)
    {
        if (state != State.Typing || !CanType()) return;
        if (!char.IsLetterOrDigit(c) && c != '-' && c != ' ') return;
        if (input.Length >= maxInputLength) return;

        input.Append(char.ToUpperInvariant(c));
        Play(keyClip);
        Redraw();
    }

    private IEnumerator Check()
    {
        state = State.Checking;
        string typed = input.ToString().Trim();

        if (string.Equals(typed, answer, System.StringComparison.OrdinalIgnoreCase))
        {
            Play(grantedClip);
            statusLine = "";
            state = State.ButtonShown;
            if (screenText != null) screenText.text = $"{header}\n\n{solvedText}";
            if (exitButton != null) exitButton.SetVisible(true);
            yield break;
        }

        mistakes++;
        Play(deniedClip);
        statusLine = wrongText;
        Redraw();
        yield return new WaitForSeconds(1.2f);

        input.Clear();
        statusLine = mistakes >= hintAfterMistakes ? hint : "";
        state = State.Typing;
        Redraw();
    }

    private void Redraw()
    {
        if (screenText == null) return;

        bool cursorOn = state == State.Typing && (Time.time * 2f) % 2f < 1f;
        var text = new StringBuilder();
        text.Append(header).Append("\n\n");
        text.Append(puzzle).Append("\n\n");
        text.Append(prompt).Append('\n');
        text.Append("> ").Append(input).Append(cursorOn ? "_" : " ");
        if (!string.IsNullOrEmpty(statusLine)) text.Append("\n\n").Append(statusLine);
        screenText.text = text.ToString();
        screenText.maxVisibleCharacters = int.MaxValue;
    }

    // ---------- Кнопка EXIT ----------

    private void OnExitPressed()
    {
        if (state != State.ButtonShown) return;

        if (dodgesDone < dodges)
        {
            Dodge();
            return;
        }

        state = State.Ending;
        exitButton.SetVisible(false);
        StartCoroutine(Ending());
    }

    private void Dodge()
    {
        string taunt = dodgeTaunts != null && dodgeTaunts.Length > 0
            ? dodgeTaunts[Mathf.Min(dodgesDone, dodgeTaunts.Length - 1)]
            : "";
        dodgesDone++;
        Play(dodgeClip);

        if (screenText != null) screenText.text = $"{header}\n\n{taunt}";

        var rect = exitButton.transform as RectTransform;
        if (rect == null || buttonArea == null) return;

        // Новое место — в другой половине экрана, чтобы курсор точно оказался не над кнопкой.
        Rect area = buttonArea.rect;
        Vector2 size = rect.rect.size;
        float halfW = Mathf.Max(0f, area.width / 2f - size.x / 2f - edgeMargin);
        float halfH = Mathf.Max(0f, area.height / 2f - size.y / 2f - edgeMargin);
        Vector2 current = rect.anchoredPosition;

        Vector2 next = current;
        for (int i = 0; i < 10 && Vector2.Distance(next, current) < area.width * 0.3f; i++)
            next = new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));

        rect.anchoredPosition = next;
    }

    // ---------- Финал ----------

    private IEnumerator Ending()
    {
        if (screenText != null) screenText.text = goodbyeText;

        if (interactor != null) interactor.InputEnabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yield return new WaitForSeconds(1.2f);

        // Рука вылезает из экрана к лицу игрока: сначала медленно, в конце — рывок.
        if (handOut != null && interactor != null && interactor.PlayerCamera != null)
        {
            handOut.gameObject.SetActive(true);
            if (handOut.TryGetComponent(out Rigidbody body)) body.isKinematic = true;
            foreach (var c in handOut.GetComponentsInChildren<Collider>()) c.enabled = false;

            Play(handClip);
            Transform cam = interactor.PlayerCamera.transform;
            Vector3 from = handOut.position;
            Quaternion fromRotation = handOut.rotation;

            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.1f, handSeconds))
            {
                float k = t * t * t;
                Vector3 to = cam.position + cam.forward * handStopDistance;
                handOut.position = Vector3.Lerp(from, to, k);
                handOut.rotation = Quaternion.Slerp(fromRotation,
                    Quaternion.LookRotation(cam.position - handOut.position, Vector3.up) * Quaternion.Euler(0f, 0f, 20f * k), k * 0.6f);
                yield return null;
            }
        }

        // Резкая чернота и надпись.
        CanvasGroup overlay = CreateEndOverlay();
        overlay.alpha = 1f;
        AudioListener.volume = 0f;
        yield return new WaitForSeconds(endHoldSeconds);
        AudioListener.volume = GameSettings.Volume;

        if (returnToMenu) GameFlow.BackToMenu();
    }

    private CanvasGroup CreateEndOverlay()
    {
        var canvasObject = new GameObject("End Overlay", typeof(Canvas), typeof(CanvasGroup));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        var background = new GameObject("Black", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        Stretch(background.GetComponent<RectTransform>());
        background.GetComponent<Image>().color = Color.black;

        var title = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        title.transform.SetParent(canvasObject.transform, false);
        Stretch(title.GetComponent<RectTransform>());
        var label = title.GetComponent<TextMeshProUGUI>();
        label.text = endTitle;
        label.fontSize = 64;
        label.characterSpacing = 12f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.93f, 0.91f, 0.83f);
        if (screenText != null) label.font = screenText.font;

        return canvasObject.GetComponent<CanvasGroup>();
    }

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
