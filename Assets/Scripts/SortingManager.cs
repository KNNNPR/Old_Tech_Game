using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Считает результаты сортировки и реагирует на ошибки.
/// Каждая ошибка зажигает следующую лампу из ряда.
/// Один объект на сцену. Приёмники (SortBin) сообщают сюда каждую карту.
/// </summary>
public class SortingManager : MonoBehaviour
{
    [Serializable]
    public class MistakeLamp
    {
        [Tooltip("Меш лампы: на нём меняется материал (необязательно).")]
        public Renderer renderer;
        [Tooltip("Какой по счёту материал меша менять (0 — первый). Нужно, если у лампы несколько материалов, например стекло и цоколь.")]
        public int materialIndex;
        [Tooltip("Источник света лампы (необязательно).")]
        public Light light;
    }

    public static SortingManager Instance { get; private set; }

    [Header("Лампы ошибок")]
    [Tooltip("По порядку: первая ошибка зажигает первую лампу и т.д.")]
    [SerializeField] private MistakeLamp[] lamps;
    [Tooltip("Материал горящей лампы (с Emission).")]
    [SerializeField] private Material lampOnMaterial;
    [Tooltip("Материал погасшей лампы.")]
    [SerializeField] private Material lampOffMaterial;

    [Header("Реакция на ошибку (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip mistakeClip;
    [Tooltip("Большая красная лампа в комнате, мигает при каждой ошибке.")]
    [SerializeField] private Light warningLight;
    [SerializeField] private float warningDuration = 2f;
    [SerializeField] private float blinkInterval = 0.2f;

    [Header("Реакция на верный ответ (необязательно)")]
    [SerializeField] private AudioClip correctClip;

    [Header("Дополнительно (настраивается в инспекторе)")]
    public UnityEvent onCorrect;
    public UnityEvent onMistake;
    [Tooltip("Загорелись все лампы.")]
    public UnityEvent onAllLampsLit;

    public int CorrectCount { get; private set; }
    public int MistakeCount { get; private set; }

    /// <summary>Карта отсортирована: (данные карты, верно ли).</summary>
    public event Action<CardData, bool> CardSorted;

    private Coroutine warningRoutine;

    private void Awake()
    {
        Instance = this;
        if (warningLight != null) warningLight.enabled = false;
        UpdateLamps();
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
            UpdateLamps();

            if (warningLight != null)
            {
                if (warningRoutine != null) StopCoroutine(warningRoutine);
                warningRoutine = StartCoroutine(WarningRoutine());
            }

            onMistake?.Invoke();

            if (lamps != null && lamps.Length > 0 && MistakeCount == lamps.Length)
                onAllLampsLit?.Invoke();
        }

        CardSorted?.Invoke(data, correct);
    }

    /// <summary>Погасить все лампы и обнулить ошибки (например, в начале новой смены).</summary>
    public void ResetMistakes()
    {
        MistakeCount = 0;
        UpdateLamps();
    }

    private void UpdateLamps()
    {
        if (lamps == null) return;

        for (int i = 0; i < lamps.Length; i++)
        {
            bool on = i < MistakeCount;
            MistakeLamp lamp = lamps[i];
            if (lamp == null) continue;

            if (lamp.renderer != null)
            {
                Material material = on ? lampOnMaterial : lampOffMaterial;
                if (material != null)
                {
                    Material[] materials = lamp.renderer.sharedMaterials;
                    int index = Mathf.Clamp(lamp.materialIndex, 0, materials.Length - 1);
                    materials[index] = material;
                    lamp.renderer.sharedMaterials = materials;
                }
            }

            if (lamp.light != null) lamp.light.enabled = on;
        }
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

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}