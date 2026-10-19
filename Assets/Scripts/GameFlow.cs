using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Сюжет игры — список шагов по порядку. Каждый шаг — либо день в основной сцене,
/// либо отдельная сцена. Advance() переходит к следующему шагу.
///
/// Порядок задан в массиве Steps ниже — чтобы переставить или добавить сцену, правь только его.
/// Номер шага живёт в статическом поле и переживает загрузку сцен.
/// Факт прохождения обучения сохраняется в PlayerPrefs и переживает перезапуск игры.
/// </summary>
public static class GameFlow
{
    // Имена сцен. Все они должны быть в File → Build Profiles → Scene List.
    public const string MenuScene = "MENU";
    public const string TutorialScene = "TUTORIAL";
    public const string MainScene = "scene_1_Kirill";
    public const string IntermissionScene = "Intermission";
    public const string Intermission2Scene = "Intermission 2";
    public const string HelpyDialogueScene = "HELPYDIALOGUE";
    public const string DeliriumScene = "DELIRIUM";
    public const string EndScene = "THE_END";

    private readonly struct Step
    {
        public readonly string scene;
        public readonly int day; // -1 — не день, а отдельная сцена

        private Step(string scene, int day) { this.scene = scene; this.day = day; }
        public static Step Day(int day) => new Step(MainScene, day);
        public static Step Scene(string scene) => new Step(scene, -1);
    }

    // ===== Порядок игры =====
    private static readonly Step[] Steps =
    {
        Step.Day(0),                      // день 1 (при первом запуске — сначала робот-экскурсия)
        Step.Scene(IntermissionScene),
        Step.Day(1),                      // день 2 — в конце разговор с роботом (прямо в основной сцене)
        Step.Scene(Intermission2Scene),
        Step.Day(2),                      // день 3 — заканчивается обмороком
        Step.Scene(DeliriumScene),
        Step.Day(3),                      // день 4 — рука
        Step.Scene(EndScene),
    };

    private const string TutorialDoneKey = "PunchCard.TutorialDone";

    public static int StepIndex { get; private set; }

    /// <summary>Номер текущего (или последнего пройденного) дня, с нуля.</summary>
    public static int CurrentShift
    {
        get
        {
            for (int i = Mathf.Min(StepIndex, Steps.Length - 1); i >= 0; i--)
                if (Steps[i].day >= 0) return Steps[i].day;
            return 0;
        }
    }

    /// <summary>Ошибки сортировки за всю игру.</summary>
    public static int TotalMistakes { get; private set; }

    public static bool TutorialDone => PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1;

    // Сброс статики при каждом запуске Play в редакторе (на случай выключенного Domain Reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StepIndex = 0;
        TotalMistakes = 0;
    }

    // ---------- Основное ----------

    /// <summary>Новая игра из меню: с первого шага.</summary>
    public static void NewGame()
    {
        StepIndex = 0;
        TotalMistakes = 0;
    }

    /// <summary>Из меню — в сцену первого шага.</summary>
    public static string SceneAfterMenu() => Steps[0].scene;

    /// <summary>Перейти к следующему шагу сюжета и загрузить его сцену.</summary>
    public static void Advance()
    {
        if(BlockSceneTransition.IsRunning)return;
        SyncWithActiveScene();
        StepIndex++;

        if (StepIndex >= Steps.Length)
        {
            Debug.Log("GameFlow: сюжет пройден, возврат в меню.");
            StepIndex = 0;
            Load(MenuScene);
            return;
        }

        Step step = Steps[StepIndex];
        Debug.Log(step.day >= 0
            ? $"GameFlow: шаг {StepIndex + 1}/{Steps.Length} — день {step.day + 1}."
            : $"GameFlow: шаг {StepIndex + 1}/{Steps.Length} — сцена «{step.scene}».");
        if(step.scene==IntermissionScene||step.scene==Intermission2Scene)BlockSceneTransition.Load(step.scene);
        else Load(step.scene);
    }

    /// <summary>Для отладки: считать, что сейчас идёт указанный день (Play прямо в основной сцене).</summary>
    public static void JumpToDay(int day)
    {
        for (int i = 0; i < Steps.Length; i++)
            if (Steps[i].day == day) { StepIndex = i; return; }
    }

    /// <summary>Возврат в меню (после финала).</summary>
    public static void BackToMenu()
    {
        StepIndex = 0;
        Load(MenuScene);
    }

    // Если игру запустили прямо из какой-то сцены (Play в редакторе), подстроить номер шага под неё.
    private static void SyncWithActiveScene()
    {
        string active = SceneManager.GetActiveScene().name;
        if (StepIndex < Steps.Length && Steps[StepIndex].scene == active) return;

        for (int i = 0; i < Steps.Length; i++)
            if (Steps[i].scene == active) { StepIndex = i; return; }
    }

    // ---------- Обучение ----------

    /// <summary>Робот закончил экскурсию: больше её не показывать.</summary>
    public static void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(TutorialDoneKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Чтобы снова увидеть обучение (для проверки).</summary>
    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialDoneKey);
        PlayerPrefs.Save();
    }

    // ---------- Совместимость со старыми вызовами ----------

    /// <summary>День закончен.</summary>
    public static void CompleteShift(int mistakes, bool wasLastShift)
    {
        TotalMistakes += mistakes;
        Advance();
    }

    public static void AfterIntermission() => Advance();
    public static void WakeUpNextDay() => Advance();

    /// <summary>Старый вариант с отдельной сценой TUTORIAL.</summary>
    public static void CompleteTutorial()
    {
        MarkTutorialDone();
        Advance();
    }

    public static void Load(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Сцена «{scene}» не добавлена в Build Settings (File → Build Profiles → Scene List).");
            return;
        }
        SceneManager.LoadScene(scene);
    }
}
