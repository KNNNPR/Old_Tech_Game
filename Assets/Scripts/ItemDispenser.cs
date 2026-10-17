using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Выдаёт предмет в лоток: принтер (бумага) или кассетный блок (кассета).
/// Создаёт префаб внутри машины в точке SpawnPoint и вставляет его в лоток (ItemSlot),
/// предмет плавно выезжает из машины к Anchor лотка.
/// </summary>
public class ItemDispenser : MonoBehaviour
{
    [Header("Что выдаём")]
    [Tooltip("Префаб с Pickupable (бумага с PrintedPaper или кассета с Cassette).")]
    [SerializeField] private GameObject prefab;
    [Tooltip("Точка внутри машины, где предмет появляется.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Лоток, куда предмет выезжает и откуда его забирает игрок.")]
    [SerializeField] private ItemSlot outputTray;

    [Header("Время")]
    [Tooltip("Машина «работает» перед тем, как предмет начнёт выезжать.")]
    [SerializeField] private float workTime = 1.5f;
    [Tooltip("Сколько предмет выезжает, пока его нельзя схватить.")]
    [SerializeField] private float slideTime = 1.5f;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip workClip;
    [SerializeField] private AudioClip outputClip;

    public bool IsBusy { get; private set; }

    /// <summary>Можно выдавать: машина свободна и лоток пуст.</summary>
    public bool IsReady => !IsBusy && outputTray != null && outputTray.Current == null;

    /// <param name="configure">Настройка созданного предмета (вставить картинку, звук и т.д.).</param>
    /// <param name="onComplete">Вызывается, когда предмет лёг в лоток.</param>
    public void Dispense(Action<GameObject> configure, Action onComplete = null)
    {
        if (!IsReady) return;
        StartCoroutine(DispenseRoutine(configure, onComplete));
    }

    private IEnumerator DispenseRoutine(Action<GameObject> configure, Action onComplete)
    {
        IsBusy = true;
        outputTray.Locked = true;

        Play(workClip);
        yield return new WaitForSeconds(workTime);

        GameObject instance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        configure?.Invoke(instance);

        Play(outputClip);
        outputTray.Insert(instance.GetComponent<Pickupable>());

        yield return new WaitForSeconds(slideTime);

        outputTray.Locked = false;
        IsBusy = false;
        onComplete?.Invoke();
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
