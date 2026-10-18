using System;
using UnityEngine;

/// <summary>
/// Кнопка EXTRACT на экране монитора. Нажимается по E, как любой интерактивный объект.
/// Вешается на всегда активный объект с коллайдером (триггером), лежащий поверх кнопки на экране.
/// Visual — дочерний UI-объект с картинкой и надписью кнопки, он включается и выключается.
/// </summary>
public class ExtractButton : MonoBehaviour, IInteractable, IRequiresFocus
{
    [Tooltip("UI-объект кнопки на экране (картинка + надпись).")]
    [SerializeField] private GameObject visual;
    [SerializeField] private string prompt = "[LMB] EXTRACT";

    public event Action Pressed;

    public bool IsVisible { get; private set; }

    private Collider[] colliders;

    public void SetVisible(bool visible)
    {
        if (colliders == null) colliders = GetComponents<Collider>();

        IsVisible = visible;
        if (visual != null) visual.SetActive(visible);
        foreach (Collider c in colliders) c.enabled = visible;
    }

    public string GetPrompt(PlayerInteractor interactor) => prompt;

    public bool CanInteract(PlayerInteractor interactor) => IsVisible;

    public void Interact(PlayerInteractor interactor) => Pressed?.Invoke();
}