using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Содержимое перфокарты. Создаётся как ассет: Project → Create → Punch Horror → Card Data.
/// Можно заполнить любое сочетание полей: текст поверх картинки, видео, звук и т.д.
/// </summary>
[CreateAssetMenu(fileName = "Card_", menuName = "Punch Horror/Card Data")]
public class CardData : ScriptableObject
{
    [Tooltip("Название для удобства в редакторе.")]
    public string cardName = "перфокарта";

    [Header("Текст (печатается по букве)")]
    [TextArea(4, 20)] public string text;

    [Header("Изображение")]
    public Texture2D image;

    [Header("Видео")]
    public VideoClip video;
    public bool loopVideo;

    [Header("Звук (играет, если нет видео)")]
    public AudioClip audio;
}
