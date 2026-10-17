using UnityEngine;

/// <summary>Распечатка. Принтер вставляет в неё изображение с карты.</summary>
[RequireComponent(typeof(Pickupable))]
public class PrintedPaper : MonoBehaviour
{
    [Tooltip("Рендерер листа, на материал которого ляжет изображение.")]
    [SerializeField] private Renderer printRenderer;

    public Texture Image { get; private set; }

    public void SetImage(Texture image)
    {
        Image = image;
        if (printRenderer != null && image != null)
            printRenderer.material.mainTexture = image;
    }
}
