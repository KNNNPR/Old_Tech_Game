using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Порядок сцен и прогресс игры между сценами.
///
/// MENU → основная сцена: день 1 (при первом запуске сначала робот-экскурсия, потом сразу карты)
///      → Intermission → основная сцена: день 2 → ... → THE_END
///
/// Номер смены живёт в статическом поле, поэтому переживает загрузку сцен.
/// Факт прохождения обучения сохраняется в PlayerPrefs и переживает перезапуск игры.
/// </summary>
public static class GameFlow
{
    // Имена сцен. Все они должны быть в File → Build Profiles / Build Settings.
    public const string MenuScene = "MENU";
    public const string TutorialScene = "TUTORIAL";
    public const string MainScene = "scene_1_Kirill";
    public const string IntermissionScene = "Intermission";
    public const string EndScene = "THE_END";

    private const string TutorialDoneKey = "PunchCard.TutorialDone";

    /// <summary>Номер текущей смены, с нуля.</summary>
    public static int CurrentShift { get; private set; }

    /// <summary>Ошибки сортировки за всю игру.</summary>
    public static int TotalMistakes { get; private set; }

    public static bool TutorialDone => PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1;

    // Сброс статики при каждом запуске Play в редакторе (на случай выключенного Domain Reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        CurrentShift = 0;
        TotalMistakes = 0;
    }

    /// <summary>Новая игра из меню: смены с начала.</summary>
    public static void NewGame()
    {
        CurrentShift = 0;
        TotalMistakes = 0;
    }

    /// <summary>Из меню всегда в основную сцену: обучение запускается прямо в ней, если ещё не пройдено.</summary>
    public static string SceneAfterMenu() => MainScene;

    /// <summary>Робот закончил экскурсию: больше её не показывать.</summary>
    public static void MarkTutorialDone()
    {
        PlayerPrefs.SetInt(TutorialDoneKey, 1);
        PlayerPrefs.Save();
    }

    /// <summary>Старый вариант с отдельной сценой TUTORIAL.</summary>
    public static void CompleteTutorial()
    {
        MarkTutorialDone();
        Load(MainScene);
    }

    /// <summary>Смена закончена: перебивка или финал.</summary>
    public static void CompleteShift(int mistakes, bool wasLastShift)
    {
        TotalMistakes += mistakes;
        CurrentShift++;
        Load(wasLastShift ? EndScene : IntermissionScene);
    }

    public static void AfterIntermission() => Load(MainScene);

    /// <summary>Чтобы снова увидеть обучение (для проверки).</summary>
    public static void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialDoneKey);
        PlayerPrefs.Save();
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
