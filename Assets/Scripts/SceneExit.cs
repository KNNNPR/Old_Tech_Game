using System.Collections;
using UnityEngine;

/// <summary>
/// Переход из перебивки (Intermission) в следующий день.
/// Ждёт, пока кат-сцена закончится (HasFinished), выдерживает паузу и загружает основную сцену.
/// Вешается на любой объект сцены Intermission. Сам находит кат-сцену.
/// (Режим Tutorial — для старой отдельной сцены TUTORIAL, сейчас не используется.)
/// </summary>
public class SceneExit : MonoBehaviour
{
    public enum After { Auto, Intermission, Tutorial }

    [Tooltip("Auto — определить по тому, что есть в сцене (кат-сцена Intermission или обучение).")]
    [SerializeField] private After after = After.Auto;

    [Tooltip("Если пусто — найдётся в сцене сам.")]
    [SerializeField] private IntermissionCinematic intermission;
    [Tooltip("Если пусто — найдётся в сцене сам.")]
    [SerializeField] private HelpyTutorialIntro tutorial;

    [Tooltip("Пауза после окончания перед загрузкой.")]
    [SerializeField] private float delay = 1f;

    [Header("Затемнение (необязательно)")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeSeconds = 1f;

    private bool leaving;

    private void Awake()
    {
        if (intermission == null) intermission = FindFirstObjectByType<IntermissionCinematic>();
        if (tutorial == null) tutorial = FindFirstObjectByType<HelpyTutorialIntro>();

        if (after == After.Auto)
            after = intermission != null ? After.Intermission : After.Tutorial;

        if (after == After.Intermission && intermission == null)
            Debug.LogError("SceneExit: в сцене нет IntermissionCinematic — переход в следующий день не случится.", this);
        if (after == After.Tutorial && tutorial == null)
            Debug.LogError("SceneExit: в сцене нет HelpyTutorialIntro — переход не случится.", this);
    }

    private void Update()
    {
        if (leaving) return;

        bool finished = after == After.Intermission
            ? intermission != null && intermission.HasFinished
            : tutorial != null && tutorial.HasFinished;

        if (finished)
        {
            leaving = true;
            StartCoroutine(Leave());
        }
    }

    private IEnumerator Leave()
    {
        yield return new WaitForSecondsRealtime(delay);

        if (fade != null)
        {
            fade.blocksRaycasts = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds))
            {
                fade.alpha = t;
                yield return null;
            }
            fade.alpha = 1f;
        }

        if (after == After.Intermission)
        {
            Debug.Log($"SceneExit: перебивка закончилась, загружаю день {GameFlow.CurrentShift + 1}.");
            GameFlow.AfterIntermission();
        }
        else
        {
            GameFlow.CompleteTutorial();
        }
    }
}