using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Считает результаты сортировки и реагирует на ошибки.
/// Один объект на сцену. Приёмники (SortBin) сообщают сюда каждую карту.
/// </summary>
public class SortingManager : MonoBehaviour
{
    public static SortingManager Instance { get; private set; }

    [Header("Реакция на ошибку (всё необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip mistakeClip;
    [Tooltip("Красная лампа, которая мигает при ошибке.")]
    [SerializeField] private Light warningLight;
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float blinkInterval = 0.2f;
    [Tooltip("Счётчик нарушений, например на табло или на стене.")]
    [SerializeField] private TMP_Text mistakesLabel;
    [SerializeField] private string mistakesFormat = "НАРУШЕНИЙ: {0}";

    [Header("Реакция на верный ответ (необязательно)")]
    [SerializeField] private AudioClip correctClip;

    [Header("Дополнительно (настраивается в инспекторе)")]
    public UnityEvent onCorrect;
    public UnityEvent onMistake;

    public int CorrectCount { get; private set; }
    public int MistakeCount { get; private set; }

    /// <summary>Карта отсортирована: (данные карты, верно ли).</summary>
    public event Action<CardData, bool> CardSorted;

    private Coroutine warningRoutine;

    private void Awake()
    {
        Instance = this;
        if (warningLight != null) warningLight.enabled = false;
        UpdateLabel();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Report(CardData data, SortVerdict verdict)
    {
        // Карта без данных: считать нечего.
        if (data == null) return;

        bool correct = (verdict == SortVerdict.Anomaly) == data.isAnomaly;

        if (correct)
        {
            CorrectCount++;
            Play(correctClip);
            onCorrect?.Invoke();
        }
        else
        {
            MistakeCount++;
            Play(mistakeClip);
            if (warningLight != null)
            {
                if (warningRoutine != null) StopCoroutine(warningRoutine);
                warningRoutine = StartCoroutine(WarningRoutine());
            }
            onMistake?.Invoke();
        }

        UpdateLabel();
        CardSorted?.Invoke(data, correct);
    }

    private IEnumerator WarningRoutine()
    {
        for (float t = 0f; t < warningDuration; t += blinkInterval)
        {
            warningLight.enabled = !warningLight.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
        warningLight.enabled = false;
    }

    private void UpdateLabel()
    {
        if (mistakesLabel != null)
            mistakesLabel.text = string.Format(mistakesFormat, MistakeCount);
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
