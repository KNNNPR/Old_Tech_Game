using UnityEngine;

public enum CardContentType
{
    Text,   // текст на мониторе
    Image,  // печать на принтере
    Audio   // запись на кассету
}

/// <summary>
/// Содержимое перфокарты. Создаётся как ассет: Project → Create → Punch Horror → Card Data.
/// </summary>
[CreateAssetMenu(fileName = "Card_", menuName = "Punch Horror/Card Data")]
public class CardData : ScriptableObject
{
    [Tooltip("Название для удобства в редакторе.")]
    public string cardName = "перфокарта";

    public CardContentType type = CardContentType.Text;

    [Tooltip("Text — сам текст на мониторе.\nImage/Audio — надпись на мониторе перед кнопкой EXTRACT (пусто — стандартная).")]
    [TextArea(4, 20)] public string text;

    [Header("Для Image")]
    public Texture2D image;

    [Header("Для Audio")]
    public AudioClip audio;
}