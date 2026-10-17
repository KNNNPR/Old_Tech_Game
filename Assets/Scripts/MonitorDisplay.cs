using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Монитор компьютера. Когда рычаг задвигает карту:
/// - Text  → текст печатается на экране;
/// - Image → надпись и кнопка EXTRACT, по нажатию принтер печатает изображение;
/// - Audio → надпись и кнопка EXTRACT, по нажатию выезжает кассета с записью.
/// </summary>
public class MonitorDisplay : MonoBehaviour
{
    [Header("Источник карт")]
    [SerializeField] private FeedLever lever;

    [Header("Экран")]
    [SerializeField] private TMP_Text screenText;
    [SerializeField] private ExtractButton extractButton;

    [Header("Устройства вывода")]
    [SerializeField] private ItemDispenser printer;
    [SerializeField] private ItemDispenser cassetteDeck;

    [Header("Звук печати текста")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeClip;
    [SerializeField, Range(0f, 1f)] private float typeVolume = 0.4f;

    [Header("Поведение")]
    [SerializeField] private float charDelay = 0.03f;
    [Tooltip("Пауза между уходом карты в машину и реакцией экрана.")]
    [SerializeField] private float startDelay = 0.8f;

    [Header("Системные надписи")]
    [TextArea, SerializeField] private string idleText = "";
    [TextArea, SerializeField] private string imageReadyText = "ОБНАРУЖЕНО ИЗОБРАЖЕНИЕ.\nНАЖМИТЕ EXTRACT ДЛЯ ПЕЧАТИ.";
    [TextArea, SerializeField] private string audioReadyText = "ОБНАРУЖЕНА АУДИОЗАПИСЬ.\nНАЖМИТЕ EXTRACT ДЛЯ ЗАПИСИ НА КАССЕТУ.";
    [SerializeField] private string extractingText = "ВЫПОЛНЯЕТСЯ...";
    [SerializeField] private string doneText = "ГОТОВО.";
    [SerializeField] private string trayBusyText = "ЛОТОК ЗАНЯТ.\nЗАБЕРИТЕ ПРЕДЫДУЩИЙ РЕЗУЛЬТАТ.";
    [SerializeField] private string errorText = "ОШИБКА ЧТЕНИЯ КАРТЫ";

    public CardData CurrentCard { get; private set; }

    /// <summary>Карта прочитана, содержимое начало показываться.</summary>
    public event Action<CardData> ContentStarted;

    /// <summary>Распечатка или кассета выданы в лоток.</summary>
    public event Action<CardData> Extracted;

    private Coroutine screenRoutine;

    private void Awake() => Clear();

    private void OnEnable()
    {
        if (lever != null) lever.CardFed += OnCardFed;
        if (extractButton != null) extractButton.Pressed += OnExtractPressed;
    }

    private void OnDisable()
    {
        if (lever != null) lever.CardFed -= OnCardFed;
        if (extractButton != null) extractButton.Pressed -= OnExtractPressed;
    }

    private void OnCardFed(Pickupable item)
    {
        PunchCard card = item != null ? item.GetComponent<PunchCard>() : null;
        Show(card != null ? card.Data : null);
    }

    /// <summary>Показать карту. null — ошибка чтения.</summary>
    public void Show(CardData data) => Run(ShowRoutine(data));

    /// <summary>Вернуть экран в исходное состояние.</summary>
    public void Clear()
    {
        StopScreen();
        CurrentCard = null;
        SetButton(false);

        if (screenText != null)
        {
            screenText.text = idleText;
            screenText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private IEnumerator ShowRoutine(CardData data)
    {
        CurrentCard = null;
        SetButton(false);
        if (screenText != null) screenText.text = "";

        yield return new WaitForSeconds(startDelay);

        if (data == null)
        {
            yield return TypeText(errorText);
            yield break;
        }

        CurrentCard = data;
        ContentStarted?.Invoke(data);

        switch (data.type)
        {
            case CardContentType.Text:
                yield return TypeText(data.text);
                break;

            case CardContentType.Image:
                yield return TypeText(OrDefault(data.text, imageReadyText));
                SetButton(true);
                break;

            case CardContentType.Audio:
                yield return TypeText(OrDefault(data.text, audioReadyText));
                SetButton(true);
                break;
        }
    }

    private void OnExtractPressed()
    {
        CardData card = CurrentCard;
        if (card == null) return;

        ItemDispenser dispenser = card.type switch
        {
            CardContentType.Image => printer,
            CardContentType.Audio => cassetteDeck,
            _ => null
        };
        if (dispenser == null) return;

        if (!dispenser.IsReady)
        {
            Run(TypeThenShowButton(trayBusyText));
            return;
        }

        SetButton(false);
        Run(TypeText(extractingText));

        dispenser.Dispense(
            instance => ConfigureOutput(instance, card),
            () =>
            {
                // Если за это время вставили новую карту, экран не трогаем.
                if (CurrentCard != card) return;

                CurrentCard = null;
                Run(TypeText(doneText));
                Extracted?.Invoke(card);
            });
    }

    private static void ConfigureOutput(GameObject instance, CardData card)
    {
        if (card.type == CardContentType.Image && instance.TryGetComponent(out PrintedPaper paper))
            paper.SetImage(card.image);
        else if (card.type == CardContentType.Audio && instance.TryGetComponent(out Cassette cassette))
            cassette.Clip = card.audio;
    }

    private IEnumerator TypeThenShowButton(string text)
    {
        SetButton(false);
        yield return TypeText(text);
        SetButton(true);
    }

    private IEnumerator TypeText(string text)
    {
        if (screenText == null) yield break;

        screenText.text = text ?? "";
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

    private void SetButton(bool visible)
    {
        if (extractButton != null) extractButton.SetVisible(visible);
    }

    private void Run(IEnumerator routine)
    {
        StopScreen();
        screenRoutine = StartCoroutine(routine);
    }

    private void StopScreen()
    {
        if (screenRoutine != null)
        {
            StopCoroutine(screenRoutine);
            screenRoutine = null;
        }
    }

    private static string OrDefault(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;
}