using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>One quiet sound per pointer entry into a usable computer-menu button.</summary>
[RequireComponent(typeof(Button))]
public sealed class MenuButtonHoverSound:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    [SerializeField] private MenuAudio menuAudio;
    [SerializeField] private ComputerMenuController computer;
    [SerializeField] private MainMenuController menu;
    private Button button;
    private bool inside;
    private void Awake()=>button=GetComponent<Button>();
    public void OnPointerEnter(PointerEventData data)
    {
        if(inside)return;inside=true;
        if(button&&button.IsActive()&&button.IsInteractable()&&menuAudio&&computer&&computer.CanUseMenu&&menu&&!menu.IsLoading&&!menu.IsRebinding)
            menuAudio.PlayHover();
    }
    public void OnPointerExit(PointerEventData data)=>inside=false;
    private void OnDisable()=>inside=false;
}
