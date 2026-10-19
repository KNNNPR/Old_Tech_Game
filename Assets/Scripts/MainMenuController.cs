using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private GameObject mainPanel, settingsPanel;
    [SerializeField] private Button startButton, settingsButton, backButton, resetButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private TMP_Text volumeValue, bindingHint, startLabel;
    [SerializeField] private Button[] bindingButtons;
    [SerializeField] private TMP_Text[] bindingLabels;
    [SerializeField] private CanvasGroup fade;
    [SerializeField] private Button exitButton;
    [SerializeField] private ComputerMenuController computerMenu;
    [SerializeField] private MenuAudio menuAudio;
    private int listening = -1;
    private int listenFrame;
    private bool loading;

    public bool SettingsOpen => settingsPanel.activeSelf;
    public bool IsRebinding => listening >= 0;
    public bool IsLoading => loading;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        GameSettings.Load();
        volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
        UpdateVolumeLabel(GameSettings.Volume);
        volumeSlider.onValueChanged.AddListener(ChangeVolume);
        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(OpenSettings);
        backButton.onClick.AddListener(CloseSettings);
        resetButton.onClick.AddListener(ResetKeys);
        if (exitButton) exitButton.onClick.AddListener(ExitGame);
        for (int i = 0; i < bindingButtons.Length; i++)
        {
            int action = i;
            bindingButtons[i].onClick.AddListener(() => BeginRebind(action));
        }
        RefreshBindings();
        settingsPanel.SetActive(false);
        fade.alpha = 1f;
        fade.blocksRaycasts = true;
        StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        while (fade.alpha > 0f) { fade.alpha = Mathf.Max(0, fade.alpha - Time.unscaledDeltaTime * 2f); yield return null; }
        fade.blocksRaycasts = false;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (loading) return;
        if (listening >= 0)
        {
            if (keyboard == null || Time.frameCount <= listenFrame) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { if(menuAudio)menuAudio.PlayBack(); CancelRebind(); return; }
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame || !GameSettings.IsAllowed(key.keyCode)) continue;
                GameSettings.Rebind((GameAction)listening, key.keyCode);
                CancelRebind();
                return;
            }
            return;
        }
        if (computerMenu != null)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) computerMenu.ReturnToOverview();
            return;
        }
        if (SettingsOpen)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CloseSettings();
            return;
        }
        Mouse mouse = Mouse.current;
        if (mouse == null || EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        Ray ray = menuCamera.ScreenPointToRay(mouse.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 40f))
        {
            FloatingLogoLetter letter = hit.collider.GetComponentInParent<FloatingLogoLetter>();
            if (letter != null)
            {
                Vector3 away = letter.transform.position - hit.point;
                away.z = -0.12f;
                if (away.sqrMagnitude < 0.001f) away = Vector3.up;
                letter.Push(away.normalized * Mathf.Min(Time.unscaledDeltaTime * 60f, 2f), mouse.delta.ReadValue());
            }
        }
    }

    public void OpenSettings()
    {
        if (computerMenu != null && !computerMenu.CanUseMenu) return;
        if(SettingsOpen)return;
        if(menuAudio)menuAudio.PlayPress();
        CancelRebind();
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(backButton.gameObject);
    }

    public void CloseSettings()=>CloseSettings(true);
    public void CloseSettings(bool playSound)
    {
        if(playSound&&SettingsOpen&&menuAudio)menuAudio.PlayBack();
        CancelRebind();
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(settingsButton.gameObject);
    }

    private void BeginRebind(int action)
    {
        if(menuAudio)menuAudio.PlayPress();
        listening = action;
        listenFrame = Time.frameCount;
        RefreshBindings();
        bindingLabels[action].text = "...";
        bindingHint.text = "PRESS A KEY   /   ESC TO CANCEL";
        // Stop Return/Space navigation from opening another row during capture.
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = false;
    }

    private void CancelRebind()
    {
        listening = -1;
        RefreshBindings();
        bindingHint.text = "SELECT A KEY TO REBIND   /   DUPLICATES SWAP";
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }

    private void ResetKeys() { if(menuAudio)menuAudio.PlayPress(); CancelRebind(); GameSettings.ResetBindings(); RefreshBindings(); }
    private void RefreshBindings()
    {
        for (int i = 0; i < bindingLabels.Length; i++) bindingLabels[i].text = GameSettings.KeyLabel((GameAction)i);
    }
    private void ChangeVolume(float value) { GameSettings.SetVolume(value); UpdateVolumeLabel(value); }
    private void UpdateVolumeLabel(float value) => volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";

    public void StartGame()
    {
        if (computerMenu != null && !computerMenu.CanUseMenu) return;
        if (!loading) { if(menuAudio)menuAudio.PlayPress(); StartCoroutine(LoadGame()); }
    }

    public void ExitGame()
    {
        if (computerMenu != null && !computerMenu.CanUseMenu) return;
        if(loading)return;
        if(menuAudio)
        {
            menuAudio.PlayPress();loading=true;
            StartCoroutine(ExitAfterPress());
        }
        else Quit();
    }

    private IEnumerator ExitAfterPress()
    {
        // Let the click sound begin before closing the application.
        yield return new WaitForSecondsRealtime(Mathf.Min(.5f,menuAudio.PressDuration));
        Quit();
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadGame()
    {
        // Первый запуск — обучение, дальше — сразу основная сцена (см. GameFlow).
        string gameScene = GameFlow.SceneAfterMenu();
        if (!Application.CanStreamedLevelBeLoaded(gameScene))
        {
            startLabel.text = "SCENE UNAVAILABLE";
            Debug.LogError("Add " + gameScene + " to Build Settings.");
            yield break;
        }
        GameFlow.NewGame();
        loading = true;
        float clickUntil=Time.unscaledTime+(menuAudio?Mathf.Min(.5f,menuAudio.PressDuration):0);
        startButton.interactable = settingsButton.interactable = false;
        fade.blocksRaycasts = true;
        while (fade.alpha < 1f) { fade.alpha = Mathf.Min(1, fade.alpha + Time.unscaledDeltaTime * 2.5f); yield return null; }
        while(Time.unscaledTime<clickUntil)yield return null;
        yield return SceneManager.LoadSceneAsync(gameScene);
    }

    private void OnDisable()
    {
        if (EventSystem.current != null) EventSystem.current.sendNavigationEvents = true;
    }
}
