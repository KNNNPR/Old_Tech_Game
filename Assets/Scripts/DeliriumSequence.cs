using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>A scene-local, irreversible oven ritual. Walking stays free until the player chooses the close view.</summary>
[DefaultExecutionOrder(200)]
public sealed class DeliriumSequence : MonoBehaviour,IInteractable
{
    public enum Phase { Approach,CardPlaced,Focusing,Locked,Blackout }
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera view;
    [SerializeField] private Transform oven,cardRest,focusPose,respawnPose;
    [SerializeField] private Pickupable offering;
    [SerializeField] private PressButton button;
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float reach=4.5f,fallLimit=-3;
    [SerializeField] private UnityEvent onFinished=new UnityEvent();
    public Phase CurrentPhase {get;private set;}
    public int PressCount {get;private set;}
    public bool HasFinished=>CurrentPhase==Phase.Blackout;
    public bool IsLocked=>CurrentPhase==Phase.Focusing||CurrentPhase==Phase.Locked||HasFinished;
    private static readonly string[] Demands=
    {
        "Destroy it.","<b>DESTROY IT.</b>","That card knows what you did.","Do not keep it. <b>PRESS AGAIN.</b>",
        "Destroy it. <i>Wash away your guilt.</i>","Your sins are still here.","Please. <b>MAKE IT STOP.</b>",
        "<b>DESTROY IT.</b> Let me forget.","I am begging you. <b>PRESS IT AGAIN.</b>","<b>ONE MORE.</b> Take it all away."
    };
    private Canvas canvas;
    private TMP_Text demand,hint,crosshair;
    private Image veil;
    private RetroSubtitleText motion;
    private Vector3 cameraLocalPosition;
    private Quaternion cameraLocalRotation;
    private float cameraFov,transitionTime,flash,pulse;
    private Vector3 transitionPosition;
    private Quaternion transitionRotation;
    private float transitionFov;
    private bool initialized,savedControl,savedController,savedInput,savedCursorVisible;
    private CursorLockMode savedCursor;
    private Vector3 cardWorldScale;
    private Vector3 buttonAim;
    private float approach,approachVelocity;
    private IEnumerator Start()
    {
        if(!interactor||!controller||!view||!oven||!cardRest||!focusPose||!respawnPose||!offering||!button)
        {Debug.LogError("DELIRIUM is missing its scene references.",this);enabled=false;yield break;}
        cameraLocalPosition=view.transform.localPosition;cameraLocalRotation=view.transform.localRotation;cameraFov=view.fieldOfView;
        savedControl=controller.ControlEnabled;savedController=controller.enabled;savedInput=interactor.InputEnabled;
        savedCursor=Cursor.lockState;savedCursorVisible=Cursor.visible;
        BuildUI();
        cardWorldScale=offering.transform.lossyScale;
        interactor.Hold(offering);
        offering.transform.localPosition=offering.HoldPositionOffset;
        offering.transform.localRotation=Quaternion.Euler(offering.HoldRotationOffset);
        offering.transform.localScale=Vector3.one*35;
        interactor.InputEnabled=false;
        interactor.BeginExternalInteraction(Tick);
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        initialized=true;
        yield return null;
    }
    public string GetPrompt(PlayerInteractor player)=>CurrentPhase==Phase.Approach?"[ E ] PLACE THE CARD":"[ LMB ] LOOK CLOSER";
    public bool CanInteract(PlayerInteractor player)=>player==interactor&&CurrentPhase==Phase.Approach&&player.HeldItem==offering;
    public void Interact(PlayerInteractor player){if(CanInteract(player))TryPlaceCard();}
    public bool TryPlaceCard()
    {
        if(CurrentPhase!=Phase.Approach||interactor.HeldItem!=offering)return false;
        interactor.ReleaseHeld();
        offering.AttachTo(cardRest,Vector3.zero,Quaternion.identity,8);
        offering.transform.localScale=cardWorldScale;
        CurrentPhase=Phase.CardPlaced;UpdateDemand();return true;
    }
    public void BeginFocus()
    {
        if(CurrentPhase!=Phase.CardPlaced)return;
        controller.SetControlEnabled(false);controller.enabled=false;
        buttonAim=buttonRenderer?buttonRenderer.bounds.center:button.transform.position;
        approach=approachVelocity=0;
        transitionPosition=view.transform.position;transitionRotation=view.transform.rotation;transitionFov=view.fieldOfView;transitionTime=0;
        CurrentPhase=Phase.Focusing;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        crosshair.enabled=false;
    }
    public void RegisterPress()
    {
        if(CurrentPhase!=Phase.Locked||PressCount>=10)return;
        PressCount++;flash=1;pulse=1;
        if(PressCount==10)
        {
            CurrentPhase=Phase.Blackout;veil.color=Color.black;motion.enabled=false;demand.enabled=hint.enabled=crosshair.enabled=false;
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            onFinished.Invoke();return;
        }
        UpdateDemand();
    }
    private void Tick()
    {
        if(!initialized)return;
        if(CurrentPhase==Phase.Blackout){interactor.SetExternalPrompt(null,"");return;}
        if(CurrentPhase==Phase.Focusing)
        {
            transitionTime+=Time.unscaledDeltaTime;
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(transitionTime/.85f));
            view.transform.SetPositionAndRotation(Vector3.Lerp(transitionPosition,focusPose.position,t),Quaternion.Slerp(transitionRotation,focusPose.rotation,t));
            view.fieldOfView=Mathf.Lerp(transitionFov,48,t);
            if(transitionTime>=.85f)CurrentPhase=Phase.Locked;
            hint.text="";interactor.SetExternalPrompt(null,"");return;
        }
        if(CurrentPhase==Phase.Locked)
        {
            bool over=Mouse.current!=null&&IsOverButton(Mouse.current.position.ReadValue());
            hint.text=over?"[ LMB ] PRESS":"PRESS THE RED BUTTON";
            if(over&&Mouse.current.leftButton.wasPressedThisFrame&&button.CanInteract(interactor))button.Interact(interactor);
            interactor.SetExternalPrompt(over?button:null,hint.text);return;
        }
        if(controller.transform.position.y<fallLimit)Respawn();
        bool near=LookingAtOven();
        hint.text=near?GetPrompt(interactor):"";
        interactor.SetExternalPrompt(near?this:null,hint.text);
        if(!near)return;
        if(CurrentPhase==Phase.Approach&&(Keyboard.current?.eKey.wasPressedThisFrame??false))TryPlaceCard();
        else if(CurrentPhase==Phase.CardPlaced&&(Mouse.current?.leftButton.wasPressedThisFrame??false))BeginFocus();
    }
    private bool LookingAtOven()
    {
        if(Vector3.Distance(view.transform.position,cardRest.position)>reach)return false;
        var ray=new Ray(view.transform.position,view.transform.forward);
        return Physics.Raycast(ray,out var hit,reach,~0,QueryTriggerInteraction.Collide)
            &&(hit.collider.transform.IsChildOf(oven)||hit.collider.transform.IsChildOf(button.transform.parent));
    }
    public bool IsOverButton(Vector2 pointer)
    {
        if(!buttonRenderer)return false;
        Bounds b=buttonRenderer.bounds;Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        for(int i=0;i<8;i++)
        {
            Vector3 p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            Vector3 screen=view.WorldToScreenPoint(p);if(screen.z<=0)return false;
            min=Vector2.Min(min,screen);max=Vector2.Max(max,screen);
        }
        return Rect.MinMaxRect(min.x-14,min.y-14,max.x+14,max.y+14).Contains(pointer);
    }
    public void Respawn()
    {
        if(IsLocked)return;
        var character=controller.GetComponent<CharacterController>();bool enabledBefore=character.enabled;
        character.enabled=false;controller.transform.SetPositionAndRotation(respawnPose.position,respawnPose.rotation);
        view.transform.localPosition=cameraLocalPosition;view.transform.localRotation=cameraLocalRotation;
        controller.SetLookDirection(respawnPose.forward);character.enabled=enabledBefore;
        Physics.SyncTransforms();if(character.enabled)character.Move(Vector3.down*.04f);
    }
    private void LateUpdate()
    {
        if(!initialized||HasFinished)return;
        float level=PressCount/10f,t=Time.unscaledTime;
        flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*2.8f);pulse=Mathf.MoveTowards(pulse,0,Time.unscaledDeltaTime*2.1f);
        if(CurrentPhase==Phase.Locked)
        {
            approach=Mathf.SmoothDamp(approach,Mathf.Min(PressCount,9)/9f,ref approachVelocity,.18f,Mathf.Infinity,Time.unscaledDeltaTime);
            Vector3 fromButton=focusPose.position-buttonAim;
            // Each press moves towards the cap, with enough clearance to keep the camera outside the oven.
            Vector3 closest=buttonAim+fromButton.normalized*Mathf.Min(.9f,fromButton.magnitude);
            Vector3 basePosition=Vector3.Lerp(focusPose.position,closest,approach);
            Quaternion baseRotation=Quaternion.Slerp(focusPose.rotation,Quaternion.LookRotation(-fromButton,Vector3.up),approach);
            float violence=level*level;
            Vector3 offset=new Vector3(Mathf.Sin(t*31),Mathf.Sin(t*39),0)*(.003f+violence*.017f+pulse*.018f);
            Vector3 angles=new Vector3(Mathf.Sin(t*23),Mathf.Sin(t*29),Mathf.Sin(t*19))*(violence*1.7f+pulse*(.45f+level*2));
            view.transform.SetPositionAndRotation(basePosition+baseRotation*offset,baseRotation*Quaternion.Euler(angles));
        }
        veil.color=new Color(.8f,.005f,.012f,Mathf.Clamp01(level*level*.48f+flash*(.06f+level*.15f)));
        if(demand&&demand.enabled)
        {
            float tick=Mathf.Floor(t*18);
            demand.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(tick*1.9f),Mathf.Sin(tick*2.4f))*(level*level*6+pulse*4);
            demand.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(tick*1.7f)*level*1.6f);
            Color color=Color.Lerp(new Color(.82f,.84f,.8f),new Color(1,.055f,.02f),level);
            if(level>.5f)color=Color.Lerp(color,new Color(1,.75f,.25f),(.5f+.5f*Mathf.Sin(t*17))*level*.35f);
            demand.color=color;
            float shimmer=.5f+.5f*Mathf.Sin(t*.85f);
            demand.colorGradient=new VertexGradient(Color.white,Color.Lerp(Color.white,new Color(.83f,.83f,.83f),shimmer),
                Color.Lerp(new Color(.28f,.28f,.28f),new Color(.43f,.43f,.43f),shimmer),
                Color.Lerp(new Color(.62f,.62f,.62f),new Color(.45f,.45f,.45f),shimmer));
        }
    }
    private void UpdateDemand()
    {
        demand.enabled=true;demand.text=Demands[Mathf.Min(PressCount,9)];demand.fontSize=25+PressCount*1.6f;
        motion.enabled=true;motion.SetFear(PressCount/9f);
    }
    private void BuildUI()
    {
        var root=new GameObject("Delirium UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=30000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var wash=CreateRect("Red pressure",root.transform,Vector2.zero,Vector2.one);veil=wash.gameObject.AddComponent<Image>();veil.color=Color.clear;veil.raycastTarget=false;
        demand=Text("Demand",root.transform,new Vector2(.08f,.77f),new Vector2(.92f,.96f),25);demand.enabled=false;
        demand.enableVertexGradient=true;
        demand.gameObject.AddComponent<Shadow>().effectColor=Color.black;
        motion=demand.gameObject.AddComponent<RetroSubtitleText>();motion.enabled=false;
        hint=Text("Action",root.transform,new Vector2(.1f,.035f),new Vector2(.9f,.1f),17);
        crosshair=Text("Crosshair",root.transform,new Vector2(.49f,.48f),new Vector2(.51f,.52f),17);crosshair.text="·";
    }
    private TMP_Text Text(string name,Transform parent,Vector2 min,Vector2 max,float size)
    {
        var text=CreateRect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();text.font=font?font:TMP_Settings.defaultFontAsset;
        text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.richText=true;text.raycastTarget=false;text.textWrappingMode=TextWrappingModes.Normal;return text;
    }
    private static RectTransform CreateRect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);
        rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;return rect;
    }
    private void OnDisable()
    {
        if(!initialized)return;
        if(interactor){interactor.EndExternalInteraction(Tick);interactor.InputEnabled=savedInput;}
        if(view){view.transform.localPosition=cameraLocalPosition;view.transform.localRotation=cameraLocalRotation;view.fieldOfView=cameraFov;}
        if(controller){controller.enabled=savedController;controller.SetControlEnabled(savedControl);}
        Cursor.lockState=savedCursor;Cursor.visible=savedCursorVisible;
        if(canvas)Destroy(canvas.gameObject);initialized=false;
    }
}
