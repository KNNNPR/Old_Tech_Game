using UnityEngine;

/// <summary>Вешается на предмет-карту рядом с Pickupable. Хранит, что карта покажет на мониторе.</summary>
[RequireComponent(typeof(Pickupable))]
public class PunchCard : MonoBehaviour
{
    [SerializeField] private CardData data;

    public CardData Data
    {
        get => data;
        set => data = value;
    }

    /// <summary>
    /// Карту просмотрели на компьютере: расшифровали текст, прочитали его
    /// или извлекли изображение/аудио. Без этого урны карту не примут.
    /// </summary>
    public bool IsViewed { get; private set; }

    public void MarkViewed() => IsViewed = true;
}