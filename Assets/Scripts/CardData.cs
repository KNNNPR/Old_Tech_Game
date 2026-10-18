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

    [Tooltip("Правильный ответ для сортировки: карта аномальная (в печь). Игрок эту пометку не видит.")]
    public bool isAnomaly;

    [Tooltip("Text — сам текст на мониторе.\nImage/Audio — надпись на мониторе перед кнопкой EXTRACT (пусто — стандартная).")]
    [TextArea(4, 20)] public string text;

    [Header("Для Text: шифр Цезаря")]
    [Tooltip("Текст приходит на экран зашифрованным, игрок подбирает сдвиг ползунком.")]
    public bool encrypted = true;
    [Tooltip("Сдвиг шифра. 0 — случайный при каждом чтении карты.")]
    public int cipherKey = 0;

    [Header("Для Image")]
    public Texture2D image;

    [Header("Для Audio")]
    [Tooltip("Запись на кассете. У аномальной карты это скрытая дорожка, которая проступает при настройке магнитофона.")]
    public AudioClip audio;
    [Tooltip("Только для аномальной карты: обычная запись, которая звучит поверх скрытой.\nПусто — случайная обычная запись из префаба кассеты.")]
    public AudioClip audioCarrier;
}