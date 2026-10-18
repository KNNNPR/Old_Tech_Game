using System.Collections;
using UnityEngine;

public enum SortVerdict
{
    Normal,   // «хорошие» — отправить
    Anomaly   // «аномальные» — в печь
}

/// <summary>
/// Приёмник карт: игрок кладёт карту в слот, жмёт кнопку —
/// карту засасывает внутрь, она уничтожается, а вердикт уходит в SortingManager.
/// </summary>
public class SortBin : MonoBehaviour
{
    [SerializeField] private ItemSlot slot;
    [SerializeField] private SortVerdict verdict = SortVerdict.Normal;

    [Header("Засасывание")]
    [Tooltip("Куда уезжает карта (в координатах родителя Anchor слота).")]
    [SerializeField] private Vector3 suckOffset = new Vector3(0f, -0.15f, 0f);
    [SerializeField] private float suckDuration = 0.8f;
    [Tooltip("Карта уменьшается, пока уезжает.")]
    [SerializeField] private bool shrink = true;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip suckClip;
    [Tooltip("Если нажали кнопку, а в приёмнике пусто.")]
    [SerializeField] private AudioClip emptyClip;

    public SortVerdict Verdict => verdict;
    public bool IsBusy { get; private set; }

    /// <summary>Повесь на On Pressed кнопки этого приёмника.</summary>
    public void Process()
    {
        if (IsBusy || slot == null) return;

        if (slot.Current == null)
        {
            Play(emptyClip);
            return;
        }

        StartCoroutine(ProcessRoutine());
    }

    private IEnumerator ProcessRoutine()
    {
        IsBusy = true;
        slot.Locked = true;

        Pickupable card = slot.Current;
        Transform anchor = slot.AnchorTransform;
        Vector3 anchorStart = anchor.localPosition;
        Vector3 startScale = card.transform.localScale;

        Play(suckClip);

        // Медленно в начале и резко в конце — как будто карту затягивает.
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, suckDuration))
        {
            float eased = t * t;
            anchor.localPosition = Vector3.Lerp(anchorStart, anchorStart + suckOffset, eased);
            if (shrink) card.transform.localScale = Vector3.Lerp(startScale, startScale * 0.1f, eased);
            yield return null;
        }

        CardData data = card.TryGetComponent(out PunchCard punchCard) ? punchCard.Data : null;

        slot.Remove();
        Destroy(card.gameObject);
        anchor.localPosition = anchorStart;

        if (SortingManager.Instance != null)
            SortingManager.Instance.Report(data, verdict);

        slot.Locked = false;
        IsBusy = false;
    }

    private void Play(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
