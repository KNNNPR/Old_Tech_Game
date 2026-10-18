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

    [Header("Шифр (для текстовых карт)")]
    [SerializeField] private CipherDial cipherDial;
    [Tooltip("Надпись со значением сдвига на экране, например «СДВИГ: 07» (необязательно).")]
    [SerializeField] private TMP_Text shiftLabel;
    // Формат надписи сдвига задаётся только в коде, чтобы старые значения из сцены не перекрывали его.
    private const string shiftLabelFormat = DefaultShiftLabelFormat;

    [Header("Карта-«отключка» (Causes Blackout)")]
    [Tooltip("Кто проигрывает потерю сознания. Если пусто — найдётся в сцене сам.")]
    [SerializeField] private PlayerFaint playerFaint;
    [Tooltip("Сколько секунд текст распадается в шум, прежде чем игрок упадёт.")]
    [SerializeField] private float glitchSeconds = 3.5f;
    [Tooltip("Звук помех на экране (необязательно, лучше зацикленный короткий шум).")]
    [SerializeField] private AudioClip glitchClip;

    [Header("Устройства вывода")]
    [Tooltip("Фотопринтер. Если задан, изображения с карт печатаются через него.")]
    [SerializeField] private PhotoPrinterSpawner photoPrinter;
    [SerializeField] private ItemDispenser printer;
    [SerializeField] private ItemDispenser cassetteDeck;
    [Tooltip("Труба с кассетами. Если задана, аудио с карт выдаётся через неё.")]
    [SerializeField] private CassetteTubeSpawner cassetteSpawner;

    [Header("Звук печати текста")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typeClip;
    [SerializeField, Range(0f, 1f)] private float typeVolume = 0.4f;

    [Header("Поведение")]
    [SerializeField] private float charDelay = 0.03f;
    [Tooltip("Пауза между уходом карты в машину и реакцией экрана.")]
    [SerializeField] private float startDelay = 0.8f;

    private const string DefaultShiftLabelFormat = "SHIFT: {0:00}";
    private const string DefaultImageReadyText = "IMAGE DETECTED.\nPRESS EXTRACT TO PRINT.";
    private const string DefaultAudioReadyText = "AUDIO DETECTED.\nPRESS EXTRACT TO RECORD TO CASSETTE.";
    private const string DefaultExtractingText = "PROCESSING...";
    private const string DefaultDoneText = "DONE.";
    private const string DefaultTrayBusyText = "OUTPUT TRAY FULL.\nREMOVE PREVIOUS OUTPUT.";
    private const string DefaultErrorText = "CARD READ ERROR";

    [Header("Системные надписи (⋮ → Use default English texts — вернуть стандартные)")]
    [TextArea, SerializeField] private string idleText = "";
    [TextArea, SerializeField] private string imageReadyText = DefaultImageReadyText;
    [TextArea, SerializeField] private string audioReadyText = DefaultAudioReadyText;
    [SerializeField] private string extractingText = DefaultExtractingText;
    [SerializeField] private string doneText = DefaultDoneText;
    [SerializeField] private string trayBusyText = DefaultTrayBusyText;
    [SerializeField] private string errorText = DefaultErrorText;

    // Значения из сцены важнее значений по умолчанию в коде,
    // поэтому старые русские надписи меняются этой кнопкой в меню компонента.
    [ContextMenu("Use default English texts")]
    private void UseDefaultEnglishTexts()
    {
        imageReadyText = DefaultImageReadyText;
        audioReadyText = DefaultAudioReadyText;
        extractingText = DefaultExtractingText;
        doneText = DefaultDoneText;
        trayBusyText = DefaultTrayBusyText;
        errorText = DefaultErrorText;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    public CardData CurrentCard { get; private set; }

    /// <summary>Карта прочитана, содержимое начало показываться.</summary>
    public event Action<CardData> ContentStarted;

    /// <summary>Распечатка или кассета выданы в лоток.</summary>
    public event Action<CardData> Extracted;

    /// <summary>Игрок подобрал верный сдвиг и текст стал читаемым.</summary>
    public event Action<CardData> TextDecoded;

    private Coroutine screenRoutine;

    private string cipherText;     // зашифрованный текст текущей карты (null — шифра нет)
    private int cipherKey;
    private bool decodedReported;

    private void Awake() => Clear();

    private void OnEnable()
    {
        if (lever != null)
        {
            lever.CardFed += OnCardFed;
            lever.CardEjected += OnCardEjected;
        }
        if (extractButton != null) extractButton.Pressed += OnExtractPressed;
        if (cipherDial != null) cipherDial.ValueChanged += OnDialChanged;
        if (photoPrinter != null) photoPrinter.PhotoReady += OnPhotoReady;
        if (cassetteSpawner != null) cassetteSpawner.CassetteReady += OnCassetteReady;
    }

    private void OnDisable()
    {
        if (lever != null)
        {
            lever.CardFed -= OnCardFed;
            lever.CardEjected -= OnCardEjected;
        }
        if (extractButton != null) extractButton.Pressed -= OnExtractPressed;
        if (cipherDial != null) cipherDial.ValueChanged -= OnDialChanged;
        if (photoPrinter != null) photoPrinter.PhotoReady -= OnPhotoReady;
        if (cassetteSpawner != null) cassetteSpawner.CassetteReady -= OnCassetteReady;
    }

    // Карту достали — экран гаснет.
    private void OnCardEjected(Pickupable item)
    {
        currentPunchCard = null;
        Clear();
    }

    private void OnCardFed(Pickupable item)
    {
        PunchCard card = item != null ? item.GetComponent<PunchCard>() : null;
        currentPunchCard = card;
        Show(card != null ? card.Data : null);
    }

    // Карта, которая сейчас в машине: ей ставится отметка «просмотрена».
    private PunchCard currentPunchCard;

    private void MarkCurrentViewed()
    {
        if (currentPunchCard != null) currentPunchCard.MarkViewed();
    }

    /// <summary>Показать карту. null — ошибка чтения.</summary>
    public void Show(CardData data) => Run(ShowRoutine(data));

    /// <summary>Вернуть экран в исходное состояние.</summary>
    public void Clear()
    {
        StopScreen();
        CurrentCard = null;
        SetButton(false);
        ResetCipher();

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
        ResetCipher();
        if (screenText != null) screenText.text = "";

        yield return new WaitForSeconds(startDelay);

        if (data == null)
        {
            yield return TypeText(errorText);
            MarkCurrentViewed();   // нечитаемую карту тоже можно выбросить
            yield break;
        }

        CurrentCard = data;
        ContentStarted?.Invoke(data);

        if (data.causesBlackout)
        {
            MarkCurrentViewed();
            yield return BlackoutRoutine(data);
            yield break;
        }

        switch (data.type)
        {
            case CardContentType.Text:
                if (data.encrypted && cipherDial != null)
                {
                    StartCipher(data);
                    yield return TypeText(CurrentDecoded());
                }
                else
                {
                    yield return TypeText(data.text);
                    MarkCurrentViewed();
                }
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

        if (card.type == CardContentType.Audio && cassetteSpawner != null)
        {
            ExtractCassette(card);
            return;
        }

        if (card.type == CardContentType.Image && photoPrinter != null)
        {
            ExtractPhoto(card);
            return;
        }

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

        MarkCurrentViewed();

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

    // ---------- Фотопринтер ----------

    private CardData pendingPhotoCard;

    private void ExtractPhoto(CardData card)
    {
        // Принтер занят, в лотке лежит прошлый лист или слишком много неубранных фото.
        if (!photoPrinter.TryPrintPhoto())
        {
            Run(TypeThenShowButton(trayBusyText));
            return;
        }

        photoPrinter.LastPrintedPhoto.AssignImage(card.image, card.isAnomaly);
        pendingPhotoCard = card;
        MarkCurrentViewed();

        SetButton(false);
        Run(TypeText(extractingText));
    }

    private void OnPhotoReady(DevelopingPhoto photo)
    {
        CardData card = pendingPhotoCard;
        if (card == null) return;
        pendingPhotoCard = null;

        // Если за это время вставили другую карту, экран не трогаем.
        if (CurrentCard != card) return;

        CurrentCard = null;
        Run(TypeText(doneText));
        Extracted?.Invoke(card);
    }

    // ---------- Кассеты ----------

    private CardData pendingCassetteCard;

    private void ExtractCassette(CardData card)
    {
        // Труба занята, в выходе лежит прошлая кассета или слишком много неубранных.
        if (!cassetteSpawner.TrySpawnCassette())
        {
            Run(TypeThenShowButton(trayBusyText));
            return;
        }

        cassetteSpawner.LastSpawnedCassette.AssignRecording(card.audio, card.isAnomaly, card.audioCarrier);
        pendingCassetteCard = card;
        MarkCurrentViewed();

        SetButton(false);
        Run(TypeText(extractingText));
    }

    private void OnCassetteReady(Cassette cassette)
    {
        CardData card = pendingCassetteCard;
        if (card == null) return;
        pendingCassetteCard = null;

        if (CurrentCard != card) return;

        CurrentCard = null;
        Run(TypeText(doneText));
        Extracted?.Invoke(card);
    }

    // ---------- Карта-«отключка» ----------

    private const string GlitchChars = "#@%&$*+=?!/\\|<>[]{}~^0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private IEnumerator BlackoutRoutine(CardData data)
    {
        if (playerFaint == null) playerFaint = FindFirstObjectByType<PlayerFaint>();
        if (screenText == null) yield break;

        string source = string.IsNullOrEmpty(data.text) ? "..." : data.text;
        if (data.encrypted) source = CaesarCipher.Shift(source, Mathf.Max(1, data.cipherKey));
        screenText.maxVisibleCharacters = int.MaxValue;

        // Красное мерцание и нарастающая тряска — всё время, пока текст распадается.
        if (playerFaint != null) playerFaint.BeginWarning(glitchSeconds);

        var chars = source.ToCharArray();
        var builder = new System.Text.StringBuilder(chars.Length * 2);
        bool faintStarted = false;

        // Шум нарастает: сначала изредка подменяются буквы, потом весь экран — случайные символы.
        // Продолжается и во время падения, до самой загрузки следующей сцены.
        for (float t = 0f; ; t += 0.05f)
        {
            float corruption = Mathf.Clamp01(t / Mathf.Max(0.1f, glitchSeconds));
            builder.Clear();

            foreach (char c in chars)
            {
                if (c == '\n' || c == ' ' && UnityEngine.Random.value > corruption) { builder.Append(c); continue; }
                builder.Append(UnityEngine.Random.value < corruption * corruption + 0.03f
                    ? GlitchChars[UnityEngine.Random.Range(0, GlitchChars.Length)]
                    : c);
            }

            // К концу добавляются лишние строки шума, заливающие экран.
            int extraLines = Mathf.FloorToInt(corruption * 10f);
            for (int line = 0; line < extraLines; line++)
            {
                builder.Append('\n');
                for (int i = 0; i < 34; i++) builder.Append(GlitchChars[UnityEngine.Random.Range(0, GlitchChars.Length)]);
            }

            screenText.text = builder.ToString();

            if (glitchClip != null && audioSource != null && UnityEngine.Random.value < 0.25f + corruption * 0.5f)
                audioSource.PlayOneShot(glitchClip, 0.2f + corruption * 0.6f);

            if (!faintStarted && corruption >= 1f)
            {
                faintStarted = true;
                if (playerFaint != null) playerFaint.Faint();
                else Debug.LogError("MonitorDisplay: карта-«отключка», но в сцене нет PlayerFaint.", this);
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // ---------- Шифр ----------

    private void StartCipher(CardData data)
    {
        cipherKey = data.cipherKey > 0
            ? data.cipherKey
            : UnityEngine.Random.Range(Mathf.Max(1, cipherDial.MinValue + 1), cipherDial.MaxValue + 1);

        cipherText = CaesarCipher.Shift(data.text, cipherKey);
        decodedReported = false;

        cipherDial.ResetValue();
        cipherDial.SetActive(true);
        UpdateShiftLabel();
    }

    private void ResetCipher()
    {
        cipherText = null;
        decodedReported = false;
        if (cipherDial != null) cipherDial.SetActive(false);
        if (shiftLabel != null) shiftLabel.text = "";
    }

    /// <summary>Текст с учётом текущего положения ползунка.</summary>
    private string CurrentDecoded() => CaesarCipher.Shift(cipherText, -cipherDial.Value);

    private void OnDialChanged(int value)
    {
        if (cipherText == null || screenText == null) return;

        // Ползунок сдвинули — печать прерывается, текст перерисовывается целиком.
        StopScreen();
        screenText.text = CurrentDecoded();
        screenText.maxVisibleCharacters = int.MaxValue;
        UpdateShiftLabel();

        if (!decodedReported && IsDecoded(value))
        {
            decodedReported = true;
            MarkCurrentViewed();
            TextDecoded?.Invoke(CurrentCard);
        }
    }

    private bool IsDecoded(int value) => cipherText != null && CurrentDecoded() == CurrentCard.text;

    private void UpdateShiftLabel()
    {
        if (shiftLabel != null && cipherDial != null)
            shiftLabel.text = string.Format(shiftLabelFormat, cipherDial.Value);
    }

    // ---------- Служебное ----------

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