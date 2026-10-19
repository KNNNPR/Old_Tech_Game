using System.Collections;
using UnityEngine;

/// <summary>
/// Переход к следующему шагу сюжета, когда сцена отыграла.
/// Сам находит в сцене то, что в ней «играет» (перебивка, разговор с роботом, DELIRIUM),
/// ждёт его окончания (HasFinished), выдерживает паузу и вызывает GameFlow.Advance().
///
/// Вешается на любой активный объект сцен: Intermission, Intermission 2, HELPYDIALOGUE, DELIRIUM.
/// </summary>
public class SceneExit : MonoBehaviour
{
    [Tooltip("Пауза после окончания сцены перед загрузкой следующей.")]
    [SerializeField] private float delay = 1f;

    [Header("Затемнение (необязательно)")]
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private float fadeSeconds = 1f;

    private IntermissionCinematic intermission;
    private HelpyReturnDialogue helpyDialogue;
    private DeliriumSequence delirium;
    private bool leaving;

    private void Awake()
    {
        intermission = FindFirstObjectByType<IntermissionCinematic>();
        helpyDialogue = FindFirstObjectByType<HelpyReturnDialogue>();
        delirium = FindFirstObjectByType<DeliriumSequence>();

        if (intermission == null && helpyDialogue == null && delirium == null)
            Debug.LogError("SceneExit: в сцене нет ни перебивки, ни разговора с роботом, ни DELIRIUM — " +
                           "не знаю, когда сцена закончится.", this);
    }

    private void Update()
    {
        if (leaving || !IsFinished()) return;
        leaving = true;
        StartCoroutine(Leave());
    }

    private bool IsFinished()
    {
        if (intermission != null) return intermission.HasFinished;
        if (helpyDialogue != null) return helpyDialogue.HasFinished;
        if (delirium != null) return delirium.HasFinished;
        return false;
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

        GameFlow.Advance();
    }
}