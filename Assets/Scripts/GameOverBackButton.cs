using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class GameOverBackButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameOverScreen screen;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Graphic underline;
    private bool inside;
    public void OnPointerEnter(PointerEventData data)
    {
        if (inside || !screen || !screen.IsReady || screen.IsLeaving) return;
        inside = true; screen.PlayHover();
        if (label) label.color = new Color(1, .39f, .32f);
        if (underline) underline.color = new Color(.85f, .14f, .11f, .85f);
    }
    public void OnPointerExit(PointerEventData data) => ResetAppearance();
    private void OnDisable() => ResetAppearance();
    private void ResetAppearance()
    {
        inside = false;
        if (label) label.color = new Color(.7f, .67f, .63f);
        if (underline) underline.color = new Color(.85f, .14f, .11f, 0);
    }
}
