using UnityEngine;

/// <summary>
/// Метка финального предмета (рука из трубы в последний день).
/// Вешается на префаб руки рядом с Pickupable. Когда предмет с этой меткой
/// вставляют в компьютер, FinalHandTrigger запускает финал.
/// </summary>
[RequireComponent(typeof(Pickupable))]
public class FinalHand : MonoBehaviour
{
}
