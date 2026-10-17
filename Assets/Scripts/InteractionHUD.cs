using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Точка прицела и подсказка взаимодействия.</summary>
public class InteractionHUD : MonoBehaviour
{
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Image crosshair;

    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color activeColor = new Color(1f, 1f, 1f, 0.9f);

    private void Update()
    {
        promptText.text = interactor.CurrentPrompt;

        if (crosshair != null)
            crosshair.color = interactor.CurrentTarget != null ? activeColor : idleColor;
    }
}
