using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [Tooltip("Особый день: вместо карт труба выдаёт эти префабы (например, руку). Если заполнено — Cards не используются,\nа день сам не заканчивается (финал запускает FinalHandTrigger).")]
        public GameObject[] spawnInsteadOfCards;

        [Header("Начало дня (необязательно)")]
        [Tooltip("Где игрок появляется в этот день (синяя ось — куда он смотрит). Пусто — где стоит в сцене.")]
        public Transform playerStart;
        [Tooltip("Предмет, который игрок держит в руке с самого начала дня (например, рука в последний день).")]
        public GameObject giveToPlayer;

        [Header("Конец дня")]
        [Tooltip("Когда все карты дня отсортированы — прилетает робот и разговаривает с игроком (Helpy Return Dialogue).")]
        public bool helpyDialogueAtEnd;

        [Header("Случайные карты (если Randomize Cards включён)")]
        [Tooltip("Сколько обычных карт вытянуть в этот день. -1 — столько, сколько обычных в списке Cards.")]
        public int randomNormals = -1;
        [Tooltip("Сколько аномальных карт вытянуть в этот день. -1 — столько, сколько аномалий в списке Cards.\n" +
                 "Карты-«отключки» (FAINT) из списка Cards добавляются в конец дня сверх этого числа.")]
        public int randomAnomalies = -1;
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
    [Tooltip("Модели робота в сцене. Прячутся всегда, кроме экскурсии и разговора (те показывают робота сами).\nЕсли пусто — найдутся сами: робот экскурсии, робот разговора и все объекты с именем robot_helper.")]
    [SerializeField] private GameObject[] robotModels;

    [Header("Заставка в начале смены (необязательно)")]
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private float titleFadeSeconds = 1f;
    [SerializeField] private float titleHoldSeconds = 2.5f;

    [Header("Разговор с роботом в конце дня")]
    [Tooltip("Разговор робота (Helpy return conversation). Если пусто — найдётся в сцене сам.\nОбъект с ним держи выключенным — его включит этот скрипт в нужный день.")]
    [SerializeField] private HelpyReturnDialogue returnDialogue;

    [Header("Случайные карты")]
    [Tooltip("Раздавать карты по дням случайно. Сколько карт в дне — берётся из его списка Cards выше.\n" +
             "Раздача делается один раз на забег (без повторов между днями) и меняется при новой игре.")]
    [SerializeField] private bool randomizeCards = true;
    [Tooltip("Из каких карт набирать дни. Пусто — из карт, уже разложенных по дням (тогда они просто перетасуются между днями).\n" +
             "Можно перетащить сюда всю папку Cards: карты-«отключки» (Causes Blackout) и учебная карта отфильтруются сами.")]
    [SerializeField] private CardData[] cardPool;
    [Tooltip("В каждом дне столько же аномалий, сколько в его исходном списке — сохраняет сложность, заданную по дням.")]
    [SerializeField] private bool keepAnomalyCount = true;
    [Tooltip("Сид для отладки: одна и та же раскладка каждый раз. 0 — случайная.")]
    [SerializeField] private int debugSeed;

    // Раздача на весь забег. Статическая, чтобы пережить перезагрузку сцены между днями.
    private static CardData[][] dealtCards;
    private static int dealtForRun = -1;

    [Header("Отладка")]
    [Tooltip("Номер дня с нуля, если нажать Play прямо в этой сцене (0 — день 1, 3 — день 4).\n-1 — как обычно. На игру из меню не влияет.")]
    [SerializeField] private int testDay = -1;

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

        // Play прямо в этой сцене с заданным днём для отладки.
        if (testDay >= 0 && Time.frameCount <= 1) GameFlow.JumpToDay(testDay);

        ShiftIndex = Mathf.Clamp(GameFlow.CurrentShift, 0, shifts.Length - 1);
        shiftCards = CardsForShift(ShiftIndex);
        if (titleGroup != null) titleGroup.alpha = 0f;

        if (tutorial == null) tutorial = FindFirstObjectByType<HelpyTutorialIntro>(FindObjectsInactive.Include);

        // Разговор робота запускается сам при включении — держим выключенным до конца нужного дня.
        if (returnDialogue == null) returnDialogue = FindFirstObjectByType<HelpyReturnDialogue>(FindObjectsInactive.Include);
        if (returnDialogue != null) returnDialogue.gameObject.SetActive(false);

        bool playTutorial = tutorial != null && ShiftIndex == 0 && (alwaysPlayTutorial || !GameFlow.TutorialDone);

        // Робот виден только во время экскурсии (её робот) и разговора (разговор включит его сам).
        GameObject tutorialRobot = playTutorial ? RobotOf(tutorial) : null;
        foreach (GameObject robot in FindRobots())
            if (robot != null && robot != tutorialRobot) robot.SetActive(false);
        if (tutorialRobot != null) tutorialRobot.SetActive(true);
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

    // ---------- Случайная раздача карт ----------

    private CardData[] CardsForShift(int index)
    {
        CardData[] authored = shifts[index].cards ?? Array.Empty<CardData>();
        if (!randomizeCards) return authored;

        if (dealtCards == null || dealtForRun != GameFlow.RunId || dealtCards.Length != shifts.Length)
        {
            dealtCards = Deal();
            dealtForRun = GameFlow.RunId;
        }
        return dealtCards[index];
    }

    /// <summary>Карта, которая не участвует в перемешивании и остаётся на своём месте в своём дне (FAINT).</summary>
    private static bool IsPinned(CardData card) => card != null && card.causesBlackout;

    private CardData[][] Deal()
    {
        var rng = new System.Random(debugSeed != 0 ? debugSeed : Environment.TickCount);

        // Колода: пул из инспектора или все карты дней. Без дублей, «отключек» и учебной карты.
        var deck = new List<CardData>();
        IEnumerable<CardData> source = cardPool != null && cardPool.Length > 0
            ? cardPool
            : shifts.SelectMany(s => s.cards ?? Array.Empty<CardData>());
        foreach (CardData card in source)
            if (card != null && !IsPinned(card) && card != tutorialCard && !deck.Contains(card))
                deck.Add(card);

        var anomalies = deck.Where(c => c.isAnomaly).ToList();
        var normals = deck.Where(c => !c.isAnomaly).ToList();
        Shuffle(anomalies, rng);
        Shuffle(normals, rng);
        Shuffle(deck, rng);

        var result = new CardData[shifts.Length][];
        var log = new System.Text.StringBuilder("ShiftManager: раздача карт на забег");
        for (int s = 0; s < shifts.Length; s++)
        {
            CardData[] authored = shifts[s].cards ?? Array.Empty<CardData>();

            // Сколько карт какого вида нужно дню: из полей смены, а если там -1 — по списку Cards
            // (закреплённые карты не считаем).
            int listAnomalies = 0, listNormals = 0;
            foreach (CardData card in authored)
            {
                if (card == null || IsPinned(card)) continue;
                if (keepAnomalyCount && card.isAnomaly) listAnomalies++;
                else listNormals++;
            }
            int needAnomalies = shifts[s].randomAnomalies >= 0 ? shifts[s].randomAnomalies : listAnomalies;
            int needNormals = shifts[s].randomNormals >= 0 ? shifts[s].randomNormals : listNormals;
            if (!keepAnomalyCount) { needNormals += needAnomalies; needAnomalies = 0; }

            var drawn = new List<CardData>();
            if (keepAnomalyCount)
            {
                Take(anomalies, normals, needAnomalies, drawn, "аномалий", s);
                Take(normals, anomalies, needNormals, drawn, "обычных", s);
            }
            else Take(deck, null, needNormals, drawn, "карт", s);
            Shuffle(drawn, rng);

            // День: вытянутые карты в случайном порядке, закреплённые (FAINT) — в конце.
            var day = new List<CardData>(drawn);
            foreach (CardData card in authored)
                if (IsPinned(card)) day.Add(card);
            result[s] = day.ToArray();

            log.Append($"\n  {shifts[s].title}: ").Append(string.Join(", ", day.Select(c => c.name)));
        }
        Debug.Log(log.ToString(), this);
        return result;

        // Вытащить count карт из main; если там не хватает — добрать из fallback.
        void Take(List<CardData> main, List<CardData> fallback, int count, List<CardData> into, string what, int shift)
        {
            for (int i = 0; i < count; i++)
            {
                List<CardData> from = main.Count > 0 ? main : fallback != null && fallback.Count > 0 ? fallback : null;
                if (from == null)
                {
                    Debug.LogWarning($"ShiftManager: в колоде не хватило {what} для {shifts[shift].title} — день будет короче. " +
                                     "Добавь карт в Card Pool.", this);
                    return;
                }
                if (from != main)
                    Debug.LogWarning($"ShiftManager: для {shifts[shift].title} не хватило {what}, взята карта другого вида.", this);

                into.Add(from[from.Count - 1]);
                from.RemoveAt(from.Count - 1);

                // Карта ушла в день — убрать её и из остальных списков, чтобы не повторилась.
                CardData taken = into[into.Count - 1];
                deck.Remove(taken); anomalies.Remove(taken); normals.Remove(taken);
            }
        }
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    [ContextMenu("Перераздать карты (следующий запуск дня)")]
    private void ResetDeal() => dealtCards = null;

    private void Start()
    {
        if (SortingManager.Instance != null) SortingManager.Instance.CardSorted += OnCardSorted;
        else Debug.LogError("ShiftManager: в сцене нет SortingManager, смена не сможет закончиться.", this);

        if (shifts == null || shifts.Length == 0) return;
        PlacePlayer(shifts[ShiftIndex].playerStart);
        GiveToPlayer(shifts[ShiftIndex].giveToPlayer);
    }

    private System.Collections.Generic.List<GameObject> FindRobots()
    {
        var result = new System.Collections.Generic.List<GameObject>();
        void Add(GameObject robot) { if (robot != null && !result.Contains(robot)) result.Add(robot); }

        if (robotModels != null) foreach (GameObject robot in robotModels) Add(robot);
        Add(RobotOf(tutorial));
        Add(RobotOf(returnDialogue));

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (string.Equals(t.name, "robot_helper", StringComparison.OrdinalIgnoreCase)) Add(t.gameObject);

        return result;
    }

    // Поле robot у скриптов робота закрытое — читаем его, не меняя их код.
    private static GameObject RobotOf(MonoBehaviour owner)
    {
        if (owner == null) return null;
        var field = owner.GetType().GetField("robot",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        return field?.GetValue(owner) is Transform robot && robot != null ? robot.gameObject : null;
    }

    private static void PlacePlayer(Transform start)
    {
        if (start == null) return;
        var player = FindFirstObjectByType<FirstPersonController>();
        if (player == null) return;

        // CharacterController не даёт двигать объект напрямую — выключаем на время переноса.
        var body = player.GetComponent<CharacterController>();
        bool wasEnabled = body != null && body.enabled;
        if (body != null) body.enabled = false;
        player.transform.SetPositionAndRotation(start.position, Quaternion.Euler(0f, start.eulerAngles.y, 0f));
        if (body != null) body.enabled = wasEnabled;
    }

    private static void GiveToPlayer(GameObject prefab)
    {
        if (prefab == null) return;
        var interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor == null || interactor.HeldItem != null) return;

        Transform cam = interactor.PlayerCamera.transform;
        GameObject instance = Instantiate(prefab, cam.position + cam.forward * 0.4f, cam.rotation);
        instance.tag = "Card"; // чтобы слот компьютера принял предмет

        if (!instance.TryGetComponent(out Pickupable item))
        {
            Debug.LogError("ShiftManager: у предмета для игрока нет Pickupable.", instance);
            return;
        }

        interactor.Hold(item);
        CenterInHand(item);
    }

    /// <summary>
    /// У моделей из Blender точка опоры (pivot) часто далеко от самой модели — тогда предмет «держится»,
    /// но висит в стороне. Сдвигаем так, чтобы в точке руки оказался центр модели, а не pivot.
    /// </summary>
    private static void CenterInHand(Pickupable item)
    {
        Transform hand = item.transform.parent;
        var renderers = item.GetComponentsInChildren<Renderer>();
        if (hand == null || renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        Quaternion rotation = Quaternion.Euler(item.HoldRotationOffset);
        Vector3 centerInItem = Vector3.Scale(item.transform.localScale, item.transform.InverseTransformPoint(bounds.center));
        Vector3 position = item.HoldPositionOffset - rotation * centerInItem;

        item.AttachTo(hand, position, rotation);
        item.SnapToTarget(); // сразу в руке, без подлёта (у руки поза своя — из FinalHand)
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
            if (ShiftIndex == 3 && shifts[ShiftIndex].giveToPlayer && shifts[ShiftIndex].giveToPlayer.GetComponent<FinalHand>())
            { tube.AutomaticSpawning = false; tube.enabled = false; shiftCards = Array.Empty<CardData>(); }
            else
            {
                GameObject[] special = shifts[ShiftIndex].spawnInsteadOfCards;
                if (special != null && special.Length > 0)
                {
                    shiftCards = Array.Empty<CardData>(); // особый день не заканчивается сортировкой
                    tube.SetPrefabs(special);
                }
                else tube.SetCardData(shiftCards);
                tube.AutomaticSpawning = true;
            }
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

    /// <summary>A fatal sorting mistake takes precedence over the normal end-of-shift transition.</summary>
    public void AbortShift()
    {
        shiftStarted = false; waitingForTutorial = false; ending = true;
        StopAllCoroutines();
        if (tube) tube.AutomaticSpawning = false;
        if (titleGroup) titleGroup.alpha = 0;
    }

    private IEnumerator EndShift()
    {
        ending = true;
        yield return new WaitForSeconds(endDelay);

        if (shifts[ShiftIndex].helpyDialogueAtEnd)
        {
            if (returnDialogue != null)
            {
                returnDialogue.gameObject.SetActive(true);
                while (!returnDialogue.HasFinished) yield return null;
                yield return new WaitForSeconds(1f);
            }
            else Debug.LogError("ShiftManager: в конце дня нужен разговор с роботом, но в сцене нет HelpyReturnDialogue.", this);
        }

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