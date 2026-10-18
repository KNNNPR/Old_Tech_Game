using UnityEngine;

/// <summary>
/// Место, к которому можно «приблизиться»: монитор, позже — магнитофон.
/// ЛКМ (или E) по нему — камера плавно встаёт в FocusPoint и фиксируется, появляется курсор.
/// Esc — выход. Вешается на объект с коллайдером, накрывающим экран.
/// </summary>
public class FocusStation : MonoBehaviour, IInteractable
{
    [Tooltip("Где окажется камера. Синяя ось Z должна смотреть на экран. Пусто — сам объект.")]
    [SerializeField] private Transform focusPoint;
    [SerializeField] private string prompt = "[LMB] Use computer";

    public Transform FocusPoint => focusPoint != null ? focusPoint : transform;
    public bool IsFocused { get; private set; }

    public void SetFocused(bool focused) => IsFocused = focused;

    public string GetPrompt(PlayerInteractor interactor) => prompt;

    public bool CanInteract(PlayerInteractor interactor) => !IsFocused;

    public void Interact(PlayerInteractor interactor) => interactor.EnterFocus(this);
}