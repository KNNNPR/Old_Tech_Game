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
}
