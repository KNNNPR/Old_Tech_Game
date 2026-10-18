using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MenuButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image rowBackground;
    [SerializeField] private Image underline;
    [SerializeField] private CanvasGroup hoverDetails;
    [SerializeField] private TMP_Text indexLabel;
    [SerializeField] private bool primaryRow;
    [SerializeField] private bool terminalPalette;
    private Vector3 home;
    private bool hovered, selected;
    private float highlight;
    private void Awake() => home = label.rectTransform.localPosition;
    public void UseTerminalPalette()
    {
        terminalPalette = true;
        ApplyColors();
    }
    public void OnPointerEnter(PointerEventData data) => hovered = true;
    public void OnPointerExit(PointerEventData data) => hovered = false;
    public void OnSelect(BaseEventData data) => selected = true;
    public void OnDeselect(BaseEventData data) => selected = false;
    private void OnDisable() { hovered = selected = false; }
    private void Update()
    {
        bool active = hovered || selected;
        highlight = Mathf.MoveTowards(highlight, active ? 1f : 0f, Time.unscaledDeltaTime * 7f);
        ApplyColors();
        label.rectTransform.localPosition = Vector3.Lerp(label.rectTransform.localPosition,
            home + Vector3.right * (active ? 9f : 0f), Time.unscaledDeltaTime * 12f);
        if (hoverDetails != null) hoverDetails.alpha = highlight;
    }

    private void ApplyColors()
    {
        Color idle = terminalPalette
            ? (primaryRow ? new Color(.4f,.95f,.56f) : new Color(.23f,.85f,.38f))
            : (primaryRow ? new Color(.93f,.91f,.83f) : new Color(.68f,.69f,.65f));
        Color hover = terminalPalette ? new Color(.65f,1f,.72f) : new Color(1f,.8f,.42f);
        label.color = Color.Lerp(idle, hover, highlight);
        if (rowBackground != null) rowBackground.color = terminalPalette
            ? new Color(.12f,.8f,.3f,Mathf.Lerp(.025f,.11f,highlight))
            : new Color(.82f,.62f,.28f,Mathf.Lerp(.025f,.11f,highlight));
        if (underline != null) underline.color = terminalPalette
            ? Color.Lerp(new Color(.15f,.55f,.25f,.26f),new Color(.45f,1f,.6f,.8f),highlight)
            : Color.Lerp(new Color(.56f,.55f,.46f,.26f),new Color(.95f,.74f,.37f,.8f),highlight);
        if (indexLabel != null) indexLabel.color = Color.Lerp(
            terminalPalette ? new Color(.15f,.55f,.25f) : new Color(.45f,.46f,.4f), hover, highlight);
    }
}
