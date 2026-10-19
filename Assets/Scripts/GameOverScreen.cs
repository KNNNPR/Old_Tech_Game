using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Fatal splash, flowing blood, then an explicit retry or quit choice.</summary>
[DisallowMultipleComponent]
public sealed class GameOverScreen : MonoBehaviour
{
    [SerializeField] private RawImage blood;
    [SerializeField] private CanvasGroup menu, fade, choices;
    [FormerlySerializedAs("backButton")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button yesButton, noButton;
    [SerializeField] private AudioSource splashAudio, musicAudio, hoverAudio;
    [FormerlySerializedAs("backAudio")]
    [SerializeField] private AudioSource pressAudio;
    [SerializeField, Min(0)] private float blackSeconds = .8f;
    [SerializeField, Min(0)] private float splashLeadSeconds = .18f;
    [SerializeField, Min(.1f)] private float bloodSeconds = 4.5f;
    [SerializeField, Min(.1f)] private float returnFadeSeconds = .9f;

    public bool IsReady { get; private set; }
    public bool IsLeaving { get; private set; }
    public bool ChoicesVisible { get; private set; }
    public float BloodProgress { get; private set; }

    private Material bloodMaterial;
    private float musicVolume, hoverVolume, splashVolume, bloodStarted;
    private bool musicStarted;
    private Vector2 choicesPosition;

    private void Awake()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        GameSettings.Load();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (!blood || !blood.texture || !blood.material || !menu || !fade || !retryButton || !choices || !yesButton || !noButton)
        {
            Debug.LogError("GAME_OVER is missing its blood image or retry controls.", this);
            enabled = false; return;
        }
        bloodMaterial = new Material(blood.material) { name = "GAME OVER blood (runtime)", hideFlags = HideFlags.HideAndDontSave };
        blood.material = bloodMaterial;
        SetBlood(0); bloodMaterial.SetFloat("_FlowTime", 0);
        menu.alpha = 0; menu.interactable = menu.blocksRaycasts = false;
        fade.alpha = 0; fade.blocksRaycasts = false;
        choicesPosition = ((RectTransform)choices.transform).anchoredPosition;
        choices.alpha = 0; choices.interactable = choices.blocksRaycasts = false;
        retryButton.interactable = yesButton.interactable = noButton.interactable = false;
        retryButton.onClick.AddListener(ShowRetryChoices);
        yesButton.onClick.AddListener(Retry);
        noButton.onClick.AddListener(ExitGame);
        foreach (var source in new[] { splashAudio, musicAudio, hoverAudio, pressAudio })
            if (source) { source.ignoreListenerVolume = false; source.dopplerLevel = 0; source.playOnAwake = false; }
        if (musicAudio) musicVolume = musicAudio.volume;
        if (hoverAudio) hoverVolume = hoverAudio.volume;
        if (splashAudio) splashVolume = splashAudio.volume;
    }
    private IEnumerator Start()
    {
        if (!bloodMaterial) yield break;
        yield return new WaitForSecondsRealtime(blackSeconds);
        if (splashAudio && splashAudio.clip) splashAudio.Play();
        StartCoroutine(MusicAfterSplash());
        yield return new WaitForSecondsRealtime(splashLeadSeconds);
        bloodStarted = Time.unscaledTime;
        for (float elapsed = 0; elapsed < bloodSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / bloodSeconds);
            SetBlood(progress);
            menu.alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .84f, progress));
            yield return null;
        }
        SetBlood(1); menu.alpha = 1;
        IsReady = true; retryButton.interactable = true;
        menu.interactable = menu.blocksRaycasts = true;
        EventSystem.current?.SetSelectedGameObject(retryButton.gameObject);
    }
    private IEnumerator MusicAfterSplash()
    {
        // The music follows the actual end of the splash, independently of text reveal.
        while (splashAudio && splashAudio.isPlaying) yield return null;
        if (musicStarted || IsLeaving || !musicAudio || !musicAudio.clip) yield break;
        musicStarted = true; musicAudio.volume = musicVolume; musicAudio.loop = true; musicAudio.Play();
    }
    private void Update()
    {
        if (bloodMaterial && BloodProgress > 0) bloodMaterial.SetFloat("_FlowTime", Time.unscaledTime - bloodStarted);
    }
    private void SetBlood(float progress)
    {
        BloodProgress = progress;
        if (bloodMaterial) bloodMaterial.SetFloat("_Progress", progress);
    }
    public void PlayHover()
    {
        if (IsReady && !IsLeaving && hoverAudio && hoverAudio.clip) hoverAudio.PlayOneShot(hoverAudio.clip);
    }
    private void PlayPress()
    {
        if (pressAudio && pressAudio.clip) pressAudio.PlayOneShot(pressAudio.clip);
    }
    public void ShowRetryChoices()
    {
        if (!IsReady || IsLeaving || ChoicesVisible) return;
        ChoicesVisible = true; retryButton.interactable = false;
        PlayPress(); StartCoroutine(RevealChoices());
    }
    private IEnumerator RevealChoices()
    {
        var rect = (RectTransform)choices.transform;
        const float duration = .3f;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            choices.alpha = t; rect.anchoredPosition = choicesPosition + Vector2.up * (22 * (1 - t));
            yield return null;
        }
        choices.alpha = 1; rect.anchoredPosition = choicesPosition;
        choices.interactable = choices.blocksRaycasts = true;
        yesButton.interactable = noButton.interactable = true;
        EventSystem.current?.SetSelectedGameObject(yesButton.gameObject);
    }
    public void Retry() => BeginLeave(false);
    public void ExitGame() => BeginLeave(true);
    private void BeginLeave(bool quit)
    {
        if (!IsReady || IsLeaving || !ChoicesVisible || !choices.interactable) return;
        string firstScene = GameFlow.SceneAfterMenu();
        if (!quit && !Application.CanStreamedLevelBeLoaded(firstScene))
        { Debug.LogError(firstScene + " must be enabled in Build Settings.", this); return; }
        IsLeaving = true;
        retryButton.interactable = yesButton.interactable = noButton.interactable = false;
        menu.interactable = menu.blocksRaycasts = choices.interactable = choices.blocksRaycasts = false;
        fade.blocksRaycasts = true;
        PlayPress(); StartCoroutine(Leave(quit, firstScene));
    }
    private IEnumerator Leave(bool quit, string firstScene)
    {
        float duration = Mathf.Max(returnFadeSeconds, pressAudio && pressAudio.clip ? pressAudio.clip.length : 0);
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / duration);
            fade.alpha = t;
            if (musicAudio) musicAudio.volume = musicVolume * (1 - t);
            if (hoverAudio) hoverAudio.volume = hoverVolume * (1 - t);
            if (splashAudio) splashAudio.volume = splashVolume * (1 - t);
            yield return null;
        }
        fade.alpha = 1;
        if (musicAudio) musicAudio.volume = 0;
        yield return null; // Render black before loading or quitting.
        if (quit)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        else
        {
            GameFlow.NewGame();
            yield return SceneManager.LoadSceneAsync(firstScene);
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (var source in new[] { splashAudio, musicAudio, hoverAudio, pressAudio }) if (source) source.Stop();
    }
    private void OnDestroy()
    {
        if (retryButton) retryButton.onClick.RemoveListener(ShowRetryChoices);
        if (yesButton) yesButton.onClick.RemoveListener(Retry);
        if (noButton) noButton.onClick.RemoveListener(ExitGame);
        if (bloodMaterial) Destroy(bloodMaterial);
    }
}
