using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class GameOverBackButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameOverScreen screen;
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text cursor;
    private bool inside;
    private Button button;
    private void Awake() => button = GetComponent<Button>();
    public void OnPointerEnter(PointerEventData data)
    {
        if (inside || !button || !button.IsInteractable() || !screen || !screen.IsReady || screen.IsLeaving) return;
        inside = true; screen.PlayHover();
        if (label) label.color = new Color(.9f,1,1);
    }
    public void OnPointerExit(PointerEventData data) => ResetAppearance();
    private void OnDisable() => ResetAppearance();
    private void ResetAppearance()
    {
        inside = false;
        if (label) label.color = new Color(.55f,.86f,.96f);
    }
    private void Update()
    {
        if (cursor) cursor.color = new Color(.45f,.9f,1,inside ? 1 : Mathf.Repeat(Time.unscaledTime,1.1f)<.7f ? .9f : .18f);
    }
}
