using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Рычаг компьютера. Работает в два хода:
///
/// Карта в слоте → рывок рычага:
///   ручка вниз → (опционально) пробивка: карта шагает, головка бьёт →
///   карта уезжает внутрь → CardFed (монитор показывает содержимое) → ручка вверх.
///   Слот остаётся закрыт, пока карта внутри.
///
/// Карта внутри → рывок рычага:
///   ручка вниз → карта выезжает обратно в слот → CardEjected (монитор гаснет) → ручка вверх.
///   Карту можно забрать из слота.
///
/// С пустым слотом и пустой машиной рычаг клацает вхолостую.
/// </summary>
public class FeedLever : MonoBehaviour, IInteractable
{
    [Header("Ссылки")]
    [Tooltip("Слот, из которого подаётся карта.")]
    [SerializeField] private ItemSlot slot;
    [Tooltip("Подвижная часть рычага (ручка). Можно оставить пустым.")]
    [SerializeField] private Transform handle;

    [Header("Движение рычага")]
    [Tooltip("На сколько градусов повернуть ручку относительно её исходного поворота.")]
    [SerializeField] private Vector3 pulledRotation = new Vector3(-45f, 0f, 0f);
    [SerializeField] private float pullDuration = 0.3f;
    [SerializeField] private float returnDuration = 0.5f;

    [Header("Пробивка")]
    [SerializeField] private bool punchBeforeFeed = true;
    [Tooltip("Головка перфоратора, которая бьёт по карте. Можно оставить пустым — тогда только звук и движение карты.")]
    [SerializeField] private Transform punchHead;
    [Tooltip("Куда опускается головка при ударе (в координатах её родителя).")]
    [SerializeField] private Vector3 punchStroke = new Vector3(0f, -0.02f, 0f);
    [SerializeField] private int punchCount = 6;
    [SerializeField] private float punchDownTime = 0.06f;
    [SerializeField] private float punchUpTime = 0.12f;
    [Tooltip("Пауза между ударами. Слегка случайная, чтобы ритм был живым.")]
    [SerializeField] private float pauseBetweenPunches = 0.15f;
    [Tooltip("На сколько карта сдвигается перед каждым ударом (в координатах родителя Anchor).")]
    [SerializeField] private Vector3 cardStep = new Vector3(0.012f, 0f, 0f);
    [SerializeField] private float cardStepTime = 0.08f;
    [Tooltip("За сколько карта возвращается в исходное положение после пробивки.")]
    [SerializeField] private float cardReturnTime = 0.5f;

    [Header("Подача и извлечение карты")]
    [Tooltip("Куда сдвинуть точку крепления карты (Anchor) в координатах её родителя.")]
    [SerializeField] private Vector3 feedOffset = new Vector3(0f, -0.12f, 0f);
    [SerializeField] private float feedDuration = 1.2f;
    [SerializeField] private float ejectDuration = 0.8f;
    [SerializeField] private bool hideCardAfterFeed = true;

    [Header("Звук (необязательно)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioSource punchAudioSource;
    [SerializeField] private AudioSource transportAudio;
    [SerializeField] private AudioClip pullClip;
    [SerializeField] private AudioClip punchClip;
    [SerializeField] private AudioClip cardStepClip;
    [SerializeField] private AudioClip feedClip;
    [SerializeField] private AudioClip ejectClip;
    [SerializeField, Range(0f, 1f)] private float pullVolume = 1f;

    public event Action FeedStarted;
    public event Action<Pickupable> PunchLanded;
    /// <summary>A story camera can hold the reading briefly before the first physical stroke.</summary>
    public bool FeedPaused { get; set; }

    /// <summary>Карта ушла в машину.</summary>
    public event Action<Pickupable> CardFed;

    /// <summary>Карта вытолкнута обратно в слот.</summary>
    public event Action<Pickupable> CardEjected;

    public bool IsBusy { get; private set; }

    /// <summary>Карта сейчас внутри машины.</summary>
    public bool HasCardInside => cardInside != null;

    private Quaternion handleRest;
    private Vector3 punchHeadRest;
    private Pickupable cardInside;

    private void Awake()
    {
        if (handle != null) handleRest = handle.localRotation;
        if (punchHead != null) punchHeadRest = punchHead.localPosition;
    }

    public string GetPrompt(PlayerInteractor interactor) =>
        HasCardInside ? "[E] Eject card" : "[E] Pull lever";

    public bool CanInteract(PlayerInteractor interactor) => !IsBusy;

    public void Interact(PlayerInteractor interactor) => StartCoroutine(PullRoutine());

    private IEnumerator PullRoutine()
    {
        IsBusy = true;
        if (slot != null) slot.Locked = true;

        Quaternion pulled = handleRest * Quaternion.Euler(pulledRotation);

        Play(pullClip, pullVolume);
        yield return RotateHandle(handleRest, pulled, pullDuration);

        if (HasCardInside)
            yield return EjectRoutine();
        else if (slot != null && slot.Current != null)
            yield return FeedRoutine(slot.Current);

        yield return RotateHandle(pulled, handleRest, returnDuration);

        // Пока карта внутри, в слот ничего не вставить.
        if (slot != null) slot.Locked = HasCardInside;
        IsBusy = false;
    }

    private IEnumerator FeedRoutine(Pickupable card)
    {
        FeedStarted?.Invoke();
        while (FeedPaused) yield return null;
        Transform anchor = slot.AnchorTransform;
        Vector3 anchorStart = anchor.localPosition;

        if (punchBeforeFeed)
            yield return PunchRoutine(anchor, anchorStart);

        Play(feedClip);
        if(transportAudio&&transportAudio.clip)transportAudio.PlayOneShot(transportAudio.clip);
        yield return MoveLocal(anchor, anchorStart, anchorStart + feedOffset, feedDuration);

        slot.Remove();
        card.transform.SetParent(null, true);
        if (hideCardAfterFeed) card.gameObject.SetActive(false);

        anchor.localPosition = anchorStart;
        cardInside = card;
        CardFed?.Invoke(card);
    }

    private IEnumerator EjectRoutine()
    {
        Pickupable card = cardInside;
        cardInside = null;

        Transform anchor = slot.AnchorTransform;
        Vector3 anchorStart = anchor.localPosition;

        // Ставим точку крепления в глубину машины и кладём туда карту.
        anchor.localPosition = anchorStart + feedOffset;
        card.gameObject.SetActive(true);
        card.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        slot.Insert(card);

        // Выталкиваем карту обратно наружу.
        Play(ejectClip);
        if(transportAudio&&transportAudio.clip)transportAudio.PlayOneShot(transportAudio.clip);
        yield return MoveLocal(anchor, anchorStart + feedOffset, anchorStart, ejectDuration);

        CardEjected?.Invoke(card);
    }

    private IEnumerator PunchRoutine(Transform anchor, Vector3 anchorStart)
    {
        for (int i = 0; i < punchCount; i++)
        {
            // Карта шагает на следующую колонку.
            Play(cardStepClip);
            yield return MoveLocal(anchor, anchor.localPosition, anchorStart + cardStep * (i + 1), cardStepTime);

            // Удар: головка вниз, звук в момент касания, головка вверх.
            if (punchHead != null)
                yield return MoveLocal(punchHead, punchHeadRest, punchHeadRest + punchStroke, punchDownTime);

            if (punchAudioSource && punchClip) { punchAudioSource.Stop(); punchAudioSource.clip = punchClip; punchAudioSource.Play(); }
            else Play(punchClip);
            PunchLanded?.Invoke(slot.Current);

            if (punchHead != null)
                yield return MoveLocal(punchHead, punchHeadRest + punchStroke, punchHeadRest, punchUpTime);

            if (pauseBetweenPunches > 0f)
                yield return new WaitForSeconds(pauseBetweenPunches * UnityEngine.Random.Range(0.6f, 1.4f));
        }

        // Карта возвращается назад перед подачей в машину.
        yield return MoveLocal(anchor, anchor.localPosition, anchorStart, cardReturnTime);
    }

    private IEnumerator RotateHandle(Quaternion from, Quaternion to, float duration)
    {
        if (handle == null) yield break;

        if (duration > 0f)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                handle.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
        handle.localRotation = to;
    }

    private static IEnumerator MoveLocal(Transform target, Vector3 from, Vector3 to, float duration)
    {
        if (duration > 0f)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                target.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
        target.localPosition = to;
    }

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, volume);
    }
}
