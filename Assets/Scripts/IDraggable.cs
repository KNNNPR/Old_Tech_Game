using UnityEngine;

/// <summary>
/// Объект, который можно тянуть мышью с зажатой ЛКМ, пока на него наведён прицел
/// (ползунки, рычажки, ручки). Пока тянешь, обзор камеры заморожен.
/// </summary>
public interface IDraggable
{
    void BeginDrag();

    /// <param name="mouseDelta">Смещение мыши за кадр в пикселях.</param>
    void Drag(Vector2 mouseDelta);

    void EndDrag();
}
