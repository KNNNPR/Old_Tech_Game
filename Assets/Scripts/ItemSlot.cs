using System;
using UnityEngine;

/// <summary>
/// Гнездо, куда можно вставить предмет из руки и забрать обратно.
/// Основа для перфоратора, считывателя, лотков и почтового ящика.
/// Машины подписываются на ItemInserted и делают свою логику.
/// </summary>
public class ItemSlot : MonoBehaviour, IInteractable
{
    [SerializeField] private string slotName = "slot";

    [Tooltip("Куда встаёт предмет. Если пусто — в центр этого объекта.")]
    [SerializeField] private Transform anchor;

    [Tooltip("Принимать только предметы с этим тегом. Пусто — принимать всё.")]
    [SerializeField] private string acceptedTag = "";

    [SerializeField] private bool allowTakeBack = true;

    [Tooltip("Скорость, с которой предмет въезжает в гнездо. 0 — обычная скорость предмета.\nДля лотка принтера поставь 1–2, чтобы лист выезжал медленно.")]
    [SerializeField] private float insertSpeed = 0f;
    [SerializeField] private AudioSource insertAudio;

    public Pickupable Current { get; private set; }

    /// <summary>Пока true, игрок не может ни вставить, ни забрать предмет (машина работает).</summary>
    public bool Locked { get; set; }

    /// <summary>
    /// Дополнительная проверка предмета. Возвращает причину отказа (её увидит игрок под прицелом)
    /// или null, если предмет можно вставить. Например, урна не берёт непросмотренную карту.
    /// </summary>
    public Func<Pickupable, string> RejectReason { get; set; }

    public event Action<Pickupable> ItemInserted;
    public event Action<Pickupable> ItemRemoved;

    private Transform Anchor => anchor != null ? anchor : transform;

    /// <summary>Точка, куда встаёт предмет (Anchor или сам слот).</summary>
    public Transform AnchorTransform => Anchor;

    public string GetPrompt(PlayerInteractor interactor)
    {
        if (Current != null) return $"[E] Take: {slotName}";

        string reason = GetRejectReason(interactor.HeldItem);
        return reason ?? $"[E] Insert: {slotName}";
    }

    private string GetRejectReason(Pickupable item) =>
        item != null && RejectReason != null ? RejectReason(item) : null;

    public bool CanInteract(PlayerInteractor interactor)
    {
        if (Locked) return false;

        if (Current == null)
            return interactor.HeldItem != null && Accepts(interactor.HeldItem);

        return allowTakeBack && interactor.HeldItem == null;
    }

    public void Interact(PlayerInteractor interactor)
    {
        if (Current == null)
        {
            // Отказ: предмет остаётся в руке, причина уже видна в подсказке.
            if (GetRejectReason(interactor.HeldItem) != null) return;

            var item = interactor.ReleaseHeld();
            Insert(item);
            if (item && Current == item && item.TryGetComponent<PunchCard>(out _) && insertAudio && insertAudio.clip)
                insertAudio.PlayOneShot(insertAudio.clip);
        }
        else
        {
            Pickupable item = Remove();
            interactor.Hold(item);
        }
    }

    public bool Accepts(Pickupable item) =>
        string.IsNullOrEmpty(acceptedTag) || item.CompareTag(acceptedTag);

    /// <summary>Вставить предмет кодом (например, машина сама кладёт карту в лоток).</summary>
    public void Insert(Pickupable item)
    {
        if (item == null || Current != null) return;

        Current = item;
        item.AttachTo(Anchor, Vector3.zero, Quaternion.identity, insertSpeed);
        ItemInserted?.Invoke(item);
    }

    /// <summary>Вынуть предмет кодом. Возвращает предмет, но не кладёт его никуда.</summary>
    public Pickupable Remove()
    {
        Pickupable item = Current;
        Current = null;
        if (item != null) ItemRemoved?.Invoke(item);
        return item;
    }
}
