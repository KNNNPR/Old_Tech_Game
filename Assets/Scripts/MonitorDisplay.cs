using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Монитор компьютера. Когда рычаг задвигает карту, показывает её содержимое:
/// текст (печатается по букве), картинку, видео или звук.
/// </summary>
public class MonitorDisplay : MonoBehaviour
{
    [Header("Источник карт")]
    [SerializeField] private FeedLever lever;

    [Header("Экран")]
    [SerializeField] private RawImage screenImage;
    [SerializeField] private TMP_Text screenText;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Vector2Int videoResolution = new Vector2Int(640, 480);

    [Header("Текст")]
    [Tooltip("Пауза между буквами.")]
    [SerializeField] private float charDelay = 0.03f;
    [Tooltip("Звук одной буквы (необязательно).")]
    [SerializeField] private AudioClip typeClip;
    [SerializeField, Range(0f, 1f)] private float typeVolume = 0.4f;

    [Header("Поведение")]
    [Tooltip("Пауза между уходом карты в машину и появлением содержимого.")]
    [SerializeField] private float startDelay = 0.8f;
    [Tooltip("Что показывать, пока карты нет.")]
    [TextArea, SerializeField] private string idleText = "";
    [SerializeField] private string errorText = "ОШИБКА ЧТЕНИЯ КАРТЫ";

    public CardData CurrentCard { get; private set; }

    /// <summary>Содержимое карты начало показываться. Пригодится для скриптовых событий.</summary>
    public event Action<CardData> ContentStarted;

    private RenderTexture videoTexture;
    private Coroutine showRoutine;

    private void Awake()
    {
        videoTexture = new RenderTexture(videoResolution.x, videoResolution.y, 0);

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = videoTexture;

            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.SetTargetAudioSource(0, audioSource);
            }
        }

        Clear();
    }

    private void OnEnable()
    {
        if (lever != null) lever.CardFed += OnCardFed;
    }

    private void OnDisable()
    {
        if (lever != null) lever.CardFed -= OnCardFed;
    }

    private void OnDestroy()
    {
        if (videoTexture != null) videoTexture.Release();
    }

    private void OnCardFed(Pickupable item)
    {
        PunchCard card = item != null ? item.GetComponent<PunchCard>() : null;
        Show(card != null ? card.Data : null);
    }

    /// <summary>Показать содержимое карты. null — показать ошибку чтения.</summary>
    public void Show(CardData data)
    {
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRoutine(data));
    }

    /// <summary>Погасить экран и остановить звук и видео.</summary>
    public void Clear()
    {
        CurrentCard = null;

        if (videoPlayer != null) videoPlayer.Stop();
        if (audioSource != null) audioSource.Stop();

        if (screenImage != null)
        {
            screenImage.texture = null;
            screenImage.enabled = false;
        }

        if (screenText != null)
        {
            screenText.text = idleText;
            screenText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private IEnumerator ShowRoutine(CardData data)
    {
        Clear();
        yield return new WaitForSeconds(startDelay);

        if (data == null)
        {
            yield return TypeText(errorText);
            yield break;
        }

        CurrentCard = data;
        ContentStarted?.Invoke(data);

        if (data.video != null && videoPlayer != null)
        {
            videoPlayer.clip = data.video;
            videoPlayer.isLooping = data.loopVideo;
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;

            screenImage.texture = videoTexture;
            screenImage.enabled = true;
            videoPlayer.Play();
        }
        else
        {
            if (data.image != null)
            {
                screenImage.texture = data.image;
                screenImage.enabled = true;
            }

            if (data.audio != null && audioSource != null)
            {
                audioSource.clip = data.audio;
                audioSource.Play();
            }
        }

        if (!string.IsNullOrEmpty(data.text))
            yield return TypeText(data.text);
    }

    private IEnumerator TypeText(string text)
    {
        if (screenText == null) yield break;

        screenText.text = text;
        screenText.maxVisibleCharacters = 0;
        screenText.ForceMeshUpdate();

        TMP_TextInfo info = screenText.textInfo;
        int total = info.characterCount;

        for (int i = 0; i < total; i++)
        {
            screenText.maxVisibleCharacters = i + 1;

            if (typeClip != null && audioSource != null && !char.IsWhiteSpace(info.characterInfo[i].character))
                audioSource.PlayOneShot(typeClip, typeVolume);

            yield return new WaitForSeconds(charDelay);
        }

        screenText.maxVisibleCharacters = int.MaxValue;
    }
}
