using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Смены (дни) в основной сцене. Каждый день — свой набор карт.
/// Если в сцене есть робот-экскурсовод и обучение ещё не пройдено, день 1 ждёт,
/// пока робот уйдёт, и только потом труба начинает выдавать карты.
/// Когда все карты дня отсортированы — перебивка или финал.
/// Один объект на основную сцену.
/// </summary>
public class ShiftManager : MonoBehaviour
{
    [Serializable]
    public class Shift
    {
        public string title = "SHIFT 1";
        [Tooltip("Карты смены по порядку, в котором они вылетят из трубы.")]
        public CardData[] cards;
    }

    [SerializeField] private Shift[] shifts;
    [SerializeField] private PunchCardTubeSpawner tube;

    [Header("Обучение с роботом (только при первом запуске)")]
    [Tooltip("Экскурсия робота в этой сцене. Если пусто — найдётся сама; если её нет — день начинается сразу.")]
    [SerializeField] private HelpyTutorialIntro tutorial;
    [Tooltip("Карта, которую робот выдаёт из трубы во время экскурсии. В счёт дня не идёт.\nПусто — труба возьмёт первый префаб из своего списка Card Prefabs.")]
    [SerializeField] private CardData tutorialCard;
    [Tooltip("Что выключить, когда обучение уже пройдено: робот, его маршрут, субтитры и т.п.")]
    [SerializeField] private GameObject[] tutorialObjects;
    [Tooltip("Показывать обучение каждый раз (для проверки).")]
    [SerializeField] private bool alwaysPlayTutorial;

    [Header("Заставка в начале смены (необязательно)")]
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private float titleFadeSeconds = 1f;
    [SerializeField] private float titleHoldSeconds = 2.5f;

    [Header("Конец смены")]
    [Tooltip("Пауза между последней отсортированной картой и переходом дальше.")]
    [SerializeField] private float endDelay = 3f;
    public UnityEvent onShiftComplete;

    public int ShiftIndex { get; private set; }
    public bool IsLastShift => shifts == null || ShiftIndex >= shifts.Length - 1;
    public int SortedCount { get; private set; }
    public int MistakesThisShift { get; private set; }

    private CardData[] shiftCards = Array.Empty<CardData>();
    private bool waitingForTutorial;
    private bool shiftStarted;
    private bool ending;

    private void Awake()
    {
        if (shifts == null || shifts.Length == 0)
        {
            Debug.LogError("ShiftManager: не задано ни одной смены.", this);
            return;
        }
        if (tube == null) Debug.LogError("ShiftManager: не задана труба с картами.", this);

        ShiftIndex = Mathf.Clamp(GameFlow.CurrentShift, 0, shifts.Length - 1);
        shiftCards = shifts[ShiftIndex].cards ?? Array.Empty<CardData>();
        if (titleGroup != null) titleGroup.alpha = 0f;

        if (tutorial == null) tutorial = FindFirstObjectByType<HelpyTutorialIntro>(FindObjectsInactive.Include);

        bool playTutorial = tutorial != null && ShiftIndex == 0 && (alwaysPlayTutorial || !GameFlow.TutorialDone);
        if (playTutorial)
        {
            // Пока идёт экскурсия, труба молчит и выдаёт только учебную карту (по запросу робота
            // из своего списка Card Prefabs). Карты дня она получит, когда робот уйдёт.
            waitingForTutorial = true;
            if (tube != null)
            {
                tube.SetCardData(tutorialCard != null ? new[] { tutorialCard } : Array.Empty<CardData>());
                tube.AutomaticSpawning = false;
                if (!tube.CanSpawn)
                    Debug.LogError("ShiftManager: трубе нечего выдать роботу. Задай Tutorial Card здесь " +
                                   "или Card Model Prefab и Card Prefabs у трубы.", this);
            }
        }
        else
        {
            // Обучение пройдено (или его нет в сцене) — прячем робота, день начинается сразу.
            if (tutorial != null) tutorial.gameObject.SetActive(false);
            if (tutorialObjects != null)
                foreach (GameObject item in tutorialObjects)
                    if (item != null) item.SetActive(false);

            // В Awake, чтобы труба получила карты до своего Start.
            BeginShift();
        }
    }

    private void Start()
    {
        if (SortingManager.Instance != null) SortingManager.Instance.CardSorted += OnCardSorted;
        else Debug.LogError("ShiftManager: в сцене нет SortingManager, смена не сможет закончиться.", this);
    }

    private void Update()
    {
        if (waitingForTutorial && tutorial != null && tutorial.HasFinished)
        {
            waitingForTutorial = false;
            GameFlow.MarkTutorialDone();
            BeginShift();
        }
    }

    private void BeginShift()
    {
        shiftStarted = true;

        if (tube != null)
        {
            tube.SetCardData(shiftCards);
            tube.AutomaticSpawning = true;
        }

        if (titleGroup != null && titleText != null)
            StartCoroutine(ShowTitle(shifts[ShiftIndex].title));
    }

    private void OnDestroy()
    {
        if (SortingManager.Instance != null) SortingManager.Instance.CardSorted -= OnCardSorted;
    }

    private void OnCardSorted(CardData data, bool correct)
    {
        // Учебная карта робота и прочие посторонние карты день не продвигают.
        if (!shiftStarted || Array.IndexOf(shiftCards, data) < 0) return;

        SortedCount++;
        if (!correct) MistakesThisShift++;

        if (!ending && SortedCount >= shiftCards.Length)
            StartCoroutine(EndShift());
    }

    private IEnumerator EndShift()
    {
        ending = true;
        yield return new WaitForSeconds(endDelay);
        onShiftComplete?.Invoke();
        GameFlow.CompleteShift(MistakesThisShift, IsLastShift);
    }

    private IEnumerator ShowTitle(string title)
    {
        titleText.text = title;
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(titleHoldSeconds);
        yield return Fade(1f, 0f);
    }

    private IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, titleFadeSeconds))
        {
            titleGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        titleGroup.alpha = to;
    }

    [ContextMenu("Показать обучение при следующем запуске")]
    private void ResetTutorialFlag()
    {
        GameFlow.ResetTutorial();
        Debug.Log("Флаг обучения сброшен: следующий Start из меню откроет TUTORIAL.");
    }
}