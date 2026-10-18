using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Финал: когда в слот компьютера вставляют руку (предмет с FinalHand) —
/// красная тревога, тряска, чернота и сцена THE_END.
/// Вешается на любой объект основной сцены.
/// </summary>
public class FinalHandTrigger : MonoBehaviour
{
    [Tooltip("Слот компьютера, куда вставляются карты.")]
    [SerializeField] private ItemSlot computerSlot;
    [Tooltip("Если пусто — найдётся в сцене сам.")]
    [SerializeField] private PlayerFaint playerFaint;

    [Tooltip("Пауза после вставки руки до начала тревоги.")]
    [SerializeField] private float delay = 1f;
    [Tooltip("Сколько длится тревога с тряской до затемнения.")]
    [SerializeField] private float alarmSeconds = 3f;
    [SerializeField] private string endScene = GameFlow.EndScene;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Звук, когда компьютер «принимает» руку.")]
    [SerializeField] private AudioClip acceptClip;

    public UnityEvent onHandInserted;

    private bool triggered;

    private void Awake()
    {
        if (playerFaint == null) playerFaint = FindFirstObjectByType<PlayerFaint>();
    }

    private void OnEnable()
    {
        if (computerSlot != null) computerSlot.ItemInserted += OnItemInserted;
        else Debug.LogError("FinalHandTrigger: не задан слот компьютера.", this);
    }

    private void OnDisable()
    {
        if (computerSlot != null) computerSlot.ItemInserted -= OnItemInserted;
    }

    private void OnItemInserted(Pickupable item)
    {
        if (triggered || item == null || !item.TryGetComponent(out FinalHand _)) return;
        triggered = true;
        StartCoroutine(EndRoutine());
    }

    private IEnumerator EndRoutine()
    {
        computerSlot.Locked = true; // руку уже не вынуть
        if (audioSource != null && acceptClip != null) audioSource.PlayOneShot(acceptClip);
        onHandInserted?.Invoke();

        yield return new WaitForSeconds(delay);

        if (playerFaint != null) playerFaint.BlackoutAndLoad(endScene, alarmSeconds);
        else GameFlow.Load(endScene);
    }
}
