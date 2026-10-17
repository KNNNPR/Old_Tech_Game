/// <summary>
/// Всё, с чем можно взаимодействовать по E: предметы, гнёзда, почтовый ящик,
/// перфоратор, телефон и т.д.
/// </summary>
public interface IInteractable
{
    /// <summary>Подсказка под прицелом, например "[E] Взять: перфокарта".</summary>
    string GetPrompt(PlayerInteractor interactor);

    /// <summary>Можно ли взаимодействовать прямо сейчас (например, рука занята).</summary>
    bool CanInteract(PlayerInteractor interactor);

    void Interact(PlayerInteractor interactor);
}
