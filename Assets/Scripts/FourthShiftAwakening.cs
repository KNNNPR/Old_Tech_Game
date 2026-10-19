using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Day-four-only waking vision, hand inspection and crimson room mood.</summary>
[DefaultExecutionOrder(500)]
public sealed class FourthShiftAwakening : MonoBehaviour
{
    [SerializeField] private ShiftManager shifts;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera view;
    [SerializeField] private Transform ovenAim;
    [SerializeField] private FinalHandTrigger finalReading;
    [SerializeField] private AudioSource heartbeat;
    [SerializeField] private Shader wakingShader;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Renderer floor;
    [SerializeField] private Texture2D ceramic, bloodCeramic;
    public bool HasAwoken { get; private set; }
    public bool IsFourthShift { get; private set; }
    private bool cameraLocked;
    private Vector3 posePosition, restLocalPosition;
    private Quaternion poseRotation;
    private Material vision;
    private RawImage eyeMask;
    private TMP_Text demand;
    private RetroSubtitleText demandMotion;
    private Canvas canvas;
    private Volume volume;
    private VolumeProfile profile;
    private FinalHand hand;
    private Material[] floorOriginals;
    private readonly List<Material> floorCopies=new List<Material>();
    private readonly Dictionary<Light,Color> lightColours=new Dictionary<Light,Color>();

    private void Awake()
    {
        IsFourthShift=shifts && shifts.ShiftIndex==3;
        if(!IsFourthShift){enabled=false;return;}
        if(!interactor||!controller||!view||!ovenAim||!wakingShader)
        {Debug.LogError("Fourth-shift awakening is missing its scene references.",this);enabled=false;return;}
        BuildUI();BuildCrimsonMood();
    }
    private IEnumerator Start()
    {
        controller.SetControlEnabled(false);controller.enabled=false;interactor.InputEnabled=false;
        interactor.SetExternalPrompt(null,"");Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        restLocalPosition=view.transform.localPosition;
        posePosition=view.transform.position;poseRotation=view.transform.rotation;cameraLocked=true;
        if(heartbeat&&heartbeat.clip){heartbeat.loop=true;heartbeat.Play();}
        float waitUntil=Time.unscaledTime+2;
        while(!interactor.HeldItem&&Time.unscaledTime<waitUntil)yield return null;
        if(interactor.HeldItem)hand=interactor.HeldItem.GetComponent<FinalHand>();
        Vector3 awakePosition=view.transform.position;
        Quaternion awakeRotation=Quaternion.LookRotation(ovenAim.position-awakePosition,Vector3.up);
        yield return new WaitForSecondsRealtime(.35f);
        for(float elapsed=0;elapsed<3;elapsed+=Time.unscaledDeltaTime)
        {
            float t=elapsed/3;float ease=Mathf.SmoothStep(0,1,t);
            posePosition=awakePosition+Vector3.down*(.55f*(1-ease));
            poseRotation=awakeRotation*Quaternion.Euler(8*(1-ease),0,-16*(1-ease));
            float blink=Mathf.Exp(-Mathf.Pow((t-.28f)/.045f,2))*.16f;
            vision.SetFloat("_Opening",Mathf.Clamp01(ease-blink));
            yield return null;
        }
        vision.SetFloat("_Opening",1);posePosition=awakePosition;
        for(float elapsed=0;elapsed<3.2f;elapsed+=Time.unscaledDeltaTime)
        {
            float t=elapsed/3.2f;
            float raise=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.2f));
            float lower=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.8f)/.2f));
            if(hand)hand.SetInspection(raise*lower,Mathf.Sin(t*Mathf.PI*2-1)*32);
            poseRotation=Quaternion.Slerp(awakeRotation,awakeRotation*Quaternion.Euler(9,0,0),Mathf.Sin(t*Mathf.PI)*.4f);
            yield return null;
        }
        if(hand)hand.SetInspection(0,0);
        cameraLocked=false;view.transform.localPosition=restLocalPosition;view.transform.localRotation=Quaternion.identity;
        controller.SetLookDirection(awakeRotation*Vector3.forward);controller.enabled=true;controller.SetControlEnabled(true);
        interactor.InputEnabled=true;HasAwoken=true;eyeMask.enabled=false;demand.enabled=true;demandMotion.enabled=true;
    }
    private void LateUpdate()
    {
        if(cameraLocked&&view)view.transform.SetPositionAndRotation(posePosition,poseRotation);
        if(!demand)return;
        demand.enabled=HasAwoken && (!finalReading || !finalReading.HasTriggered);
        demandMotion.enabled=demand.enabled;
        if(!demand.enabled)return;
        float t=Time.unscaledTime;float tick=Mathf.Floor(t*16);
        demand.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(tick*.31f)*9,Mathf.Sin(tick*.19f)*3);
        float pulse=.5f+.5f*Mathf.Sin(t*2.6f);
        demand.colorGradient=new VertexGradient(new Color(1,.08f,.12f),new Color(1,.25f*pulse,.2f),
            new Color(.25f,.002f,.008f),new Color(.6f,.015f,.035f));
    }
    private void BuildUI()
    {
        var root=new GameObject("Fourth shift vision",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));root.transform.SetParent(transform,false);
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=31000;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
        var mask=new GameObject("Waking eyelids",typeof(RectTransform),typeof(RawImage));mask.transform.SetParent(root.transform,false);
        var rect=mask.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        vision=new Material(wakingShader){hideFlags=HideFlags.HideAndDontSave};vision.SetFloat("_Opening",0);
        eyeMask=mask.GetComponent<RawImage>();eyeMask.material=vision;eyeMask.texture=Texture2D.whiteTexture;eyeMask.raycastTarget=false;
        var label=new GameObject("READ THE HAND",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(root.transform,false);
        rect=label.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.1f,.82f);rect.anchorMax=new Vector2(.9f,.94f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        demand=label.GetComponent<TMP_Text>();demand.font=font?font:TMP_Settings.defaultFontAsset;demand.fontSize=40;demand.fontStyle=FontStyles.Bold;
        demand.text="READ THE HAND";demand.characterSpacing=4;demand.alignment=TextAlignmentOptions.Center;demand.enableVertexGradient=true;demand.raycastTarget=false;demand.enabled=false;
        label.AddComponent<Shadow>().effectColor=new Color(.08f,0,0,.95f);
        demandMotion=label.AddComponent<RetroSubtitleText>();demandMotion.SetFear(.55f);demandMotion.enabled=false;
    }
    private void BuildCrimsonMood()
    {
        profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Fourth shift crimson";profile.hideFlags=HideFlags.HideAndDontSave;
        var colour=profile.Add<ColorAdjustments>(true);colour.colorFilter.Override(new Color(1,.48f,.46f));colour.saturation.Override(-12);colour.contrast.Override(8);
        var vignette=profile.Add<Vignette>(true);vignette.color.Override(new Color(.12f,.002f,.008f));vignette.intensity.Override(.34f);vignette.smoothness.Override(.7f);
        volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.profile=profile;
        foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var light in root.GetComponentsInChildren<Light>(true))
        {lightColours[light]=light.color;light.color=Color.Lerp(light.color,new Color(1,.07f,.12f),.65f);}
        if(floor&&ceramic&&bloodCeramic)
        {
            floorOriginals=floor.sharedMaterials;var materials=(Material[])floorOriginals.Clone();
            for(int i=0;i<materials.Length;i++)
            {
                var original=materials[i];if(!original)continue;
                string property=original.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                if(!original.HasProperty(property)||original.GetTexture(property)!=ceramic)continue;
                var copy=new Material(original){name="Fourth shift blood ceramic",hideFlags=HideFlags.HideAndDontSave};
                copy.SetTexture(property,bloodCeramic);materials[i]=copy;floorCopies.Add(copy);
            }
            floor.sharedMaterials=materials;
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines();if(demandMotion)demandMotion.enabled=false;if(heartbeat)heartbeat.Stop();if(hand)hand.SetInspection(0,0);
        foreach(var pair in lightColours)if(pair.Key)pair.Key.color=pair.Value;
        if(floor&&floorOriginals!=null)floor.sharedMaterials=floorOriginals;
        foreach(var material in floorCopies)if(material)Destroy(material);
        if(volume)Destroy(volume);if(profile)Destroy(profile);if(vision)Destroy(vision);if(canvas)Destroy(canvas.gameObject);
    }
}
