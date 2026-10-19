using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Сцена TITLES: на чёрном фоне картинка END под музыку.
/// Пробел — пропустить. Когда музыка закончилась (или пропустили) — затухание и главное меню.
/// Вешается на любой объект сцены TITLES. Канвас строит сам.
/// </summary>
public class TitlesScreen : MonoBehaviour
{
    [Tooltip("Картинка END (Texture Type: Default или Sprite — любая).")]
    [SerializeField] private Texture2D endImage;
    [Tooltip("Доля высоты экрана, которую занимает картинка (пропорции сохраняются).")]
    [SerializeField, Range(0.1f, 1f)] private float imageHeight = 0.6f;
    [Tooltip("Музыка титров (credits idk).")]
    [SerializeField] private AudioClip music;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.8f;
    [Tooltip("Сколько показывать, если музыки нет (сек). С музыкой — пока она играет.")]
    [SerializeField] private float durationWithoutMusic = 10f;
    [SerializeField] private float fadeInSeconds = 2f;
    [SerializeField] private float fadeOutSeconds = 1.5f;
    [SerializeField] private string skipHint = "[SPACE] Skip";
    [SerializeField] private TMP_FontAsset hintFont;

    private AudioSource source;
    private CanvasGroup group;
    private bool leaving;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        AudioListener.volume = GameSettings.Volume;

        Build();

        if (music != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.clip = music;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
        }

        StartCoroutine(Run());
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (!leaving && kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            StopAllCoroutines();
            StartCoroutine(Leave());
        }
    }

    private IEnumerator Run()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInSeconds))
        {
            group.alpha = t;
            if (source) source.volume = musicVolume * t;
            yield return null;
        }
        group.alpha = 1f;
        if (source) source.volume = musicVolume;

        if (source) while (source.isPlaying) yield return null;
        else yield return new WaitForSecondsRealtime(durationWithoutMusic);

        yield return Leave();
    }

    private IEnumerator Leave()
    {
        leaving = true;
        float startAlpha = group.alpha;
        float startVolume = source ? source.volume : 0f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeOutSeconds))
        {
            group.alpha = startAlpha * (1f - t);
            if (source) source.volume = startVolume * (1f - t);
            yield return null;
        }
        GameFlow.BackToMenu();
    }

    private void Build()
    {
        var root = new GameObject("Titles", typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;

        var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
        black.transform.SetParent(root.transform, false);
        Stretch(black.GetComponent<RectTransform>());
        black.GetComponent<Image>().color = Color.black;

        var content = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
        content.transform.SetParent(root.transform, false);
        Stretch(content.GetComponent<RectTransform>());
        group = content.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        if (endImage != null)
        {
            var picture = new GameObject("END", typeof(RectTransform), typeof(RawImage));
            picture.transform.SetParent(content.transform, false);
            var rect = picture.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            float height = 1080f * imageHeight;
            float aspect = endImage.height > 0 ? endImage.width / (float)endImage.height : 1f;
            rect.sizeDelta = new Vector2(height * aspect, height);
            var raw = picture.GetComponent<RawImage>();
            raw.texture = endImage;
            raw.raycastTarget = false;
        }
        else Debug.LogWarning("TitlesScreen: не задана картинка END.", this);

        if (!string.IsNullOrEmpty(skipHint))
        {
            var hint = new GameObject("Skip hint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hint.transform.SetParent(root.transform, false);
            var rect = hint.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(500, 50);
            rect.anchoredPosition = new Vector2(-30, 24);
            var text = hint.GetComponent<TextMeshProUGUI>();
            text.text = skipHint;
            text.fontSize = 22;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = new Color(1f, 1f, 1f, 0.35f);
            text.raycastTarget = false;
            if (hintFont) text.font = hintFont;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
