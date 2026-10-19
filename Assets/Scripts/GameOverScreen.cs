using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Blackout, fatal splash, creeping blood and an explicit return to the main menu.</summary>
[DisallowMultipleComponent]
public sealed class GameOverScreen : MonoBehaviour
{
    [SerializeField] private RawImage blood;
    [SerializeField] private CanvasGroup menu, fade;
    [SerializeField] private Button backButton;
    [SerializeField] private AudioSource splashAudio, musicAudio, hoverAudio, backAudio;
    [SerializeField, Min(0)] private float blackSeconds = .8f;
    [SerializeField, Min(0)] private float splashLeadSeconds = .18f;
    [SerializeField, Min(.1f)] private float bloodSeconds = 4.5f;
    [SerializeField, Min(.1f)] private float returnFadeSeconds = .9f;

    public bool IsReady { get; private set; }
    public bool IsLeaving { get; private set; }
    public float BloodProgress { get; private set; }

    private Material bloodMaterial;
    private float musicVolume, hoverVolume, splashVolume, bloodStarted;
    private bool musicStarted;

    private void Awake()
    {
        Time.timeScale = 1; AudioListener.pause = false;
        GameSettings.Load();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (!blood || !blood.texture || !blood.material || !menu || !fade || !backButton)
        {
            Debug.LogError("GAME_OVER is missing its blood image or menu references.", this);
            enabled = false; return;
        }
        bloodMaterial = new Material(blood.material) { name = "GAME OVER blood (runtime)", hideFlags = HideFlags.HideAndDontSave };
        blood.material = bloodMaterial;
        SetBlood(0); bloodMaterial.SetFloat("_FlowTime", 0);
        menu.alpha = 0; menu.interactable = menu.blocksRaycasts = false;
        fade.alpha = 0; fade.blocksRaycasts = false;
        backButton.interactable = false;
        backButton.onClick.AddListener(ReturnToMenu);
        foreach (var source in new[] { splashAudio, musicAudio, hoverAudio, backAudio })
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
        yield return new WaitForSecondsRealtime(splashLeadSeconds);
        bloodStarted = Time.unscaledTime;
        for (float elapsed = 0; elapsed < bloodSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float progress = Mathf.Clamp01(elapsed / bloodSeconds);
            SetBlood(progress);
            float text = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .84f, progress));
            menu.alpha = text;
            if (text > 0) StartMusic();
            if (musicAudio) musicAudio.volume = musicVolume * text;
            yield return null;
        }
        SetBlood(1); menu.alpha = 1; StartMusic();
        if (musicAudio) musicAudio.volume = musicVolume;
        IsReady = true; backButton.interactable = true;
        menu.interactable = menu.blocksRaycasts = true;
        EventSystem.current?.SetSelectedGameObject(backButton.gameObject);
    }
    private void StartMusic()
    {
        if (musicStarted || !musicAudio || !musicAudio.clip) return;
        musicStarted = true; musicAudio.volume = 0; musicAudio.loop = true; musicAudio.Play();
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
    public void ReturnToMenu()
    {
        if (!IsReady || IsLeaving) return;
        if (!Application.CanStreamedLevelBeLoaded(GameFlow.MenuScene))
        { Debug.LogError("MENU must be enabled in Build Settings.", this); return; }
        IsLeaving = true; backButton.interactable = false;
        menu.interactable = menu.blocksRaycasts = false; fade.blocksRaycasts = true;
        if (backAudio && backAudio.clip) backAudio.Play();
        StartCoroutine(Leave());
    }
    private IEnumerator Leave()
    {
        float duration = Mathf.Max(returnFadeSeconds, backAudio && backAudio.clip ? backAudio.clip.length : 0);
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
        // Render a completely black frame before activating the next scene.
        yield return null;
        yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (var source in new[] { splashAudio, musicAudio, hoverAudio, backAudio }) if (source) source.Stop();
    }
    private void OnDestroy()
    {
        if (backButton) backButton.onClick.RemoveListener(ReturnToMenu);
        if (bloodMaterial) Destroy(bloodMaterial);
    }
}
