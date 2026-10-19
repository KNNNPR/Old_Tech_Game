using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scene-local credits on black, scrolling down with the supplied studio logo.</summary>
public sealed class EndCreditsRoll:MonoBehaviour
{
    [SerializeField] private Texture2D logo;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private AudioSource music;
    [SerializeField,Min(5)] private float scrollSeconds=36;
    [SerializeField] private bool returnToMenu=true;
    public bool IsPlaying {get;private set;}
    public float ScrollProgress {get;private set;}
    private Canvas canvas;
    private CanvasGroup textGroup;
    private RectTransform content;
    private float musicVolume;
    private static readonly Color Gold=new Color(.64f,.50f,.27f);
    public void Play()
    {
        if(IsPlaying)return;IsPlaying=true;Build();
        if(music&&music.clip){musicVolume=music.volume;music.volume=0;music.loop=true;music.Play();}
        StartCoroutine(Roll());
    }
    private IEnumerator Roll()
    {
        for(float elapsed=0;elapsed<scrollSeconds;elapsed+=Time.unscaledDeltaTime)
        {
            ScrollProgress=Mathf.Clamp01(elapsed/scrollSeconds);
            content.anchoredPosition=new Vector2(0,Mathf.Lerp(1200,-1200,ScrollProgress));
            textGroup.alpha=Mathf.SmoothStep(0,1,elapsed/.7f);
            if(music)music.volume=musicVolume*Mathf.SmoothStep(0,1,elapsed/1.2f);
            yield return null;
        }
        ScrollProgress=1;
        for(float t=0;t<1;t+=Time.unscaledDeltaTime)
        {textGroup.alpha=1-t;if(music)music.volume=musicVolume*(1-t);yield return null;}
        if(music)music.Stop();IsPlaying=false;
        if(returnToMenu)GameFlow.BackToMenu();
    }
    private void Build()
    {
        var root=new GameObject("AppleJollyGarden credits",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32765;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var background=new GameObject("Black",typeof(RectTransform),typeof(Image));background.transform.SetParent(root.transform,false);
        var rect=background.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;background.GetComponent<Image>().color=Color.black;
        var textRoot=new GameObject("Credits text",typeof(RectTransform),typeof(CanvasGroup));textRoot.transform.SetParent(root.transform,false);rect=textRoot.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        textGroup=textRoot.GetComponent<CanvasGroup>();textGroup.alpha=0;textGroup.blocksRaycasts=false;
        content=Rect(textRoot.transform,"Scrolling credits",0,0,1920,1080);content.anchoredPosition=new Vector2(0,1200);
        var logoRect=Rect(content,"AppleJollyGarden logo",0,50,252,252);var picture=logoRect.gameObject.AddComponent<RawImage>();picture.texture=logo;picture.raycastTarget=false;
        Label(content,"Thanks","THANK YOU FOR PLAYING\nPUNCH CARD 0",0,355,1700,120,50,Gold,true);
        Label(content,"Message","WE DID BEST FOR YOUR DAY",0,495,1700,60,27,Color.white,true);
        Column("ZHELE","SOUND DESIGN\nMUSIC",-595);
        Column("APPLEJOLLYGARDEN","3D ART\nGAME DESIGN\nEFFECTS\nSHADERS",0);
        Column("KNNPR","CODE\nSCENE SETTINGS",595);
        Label(content,"Apple motto","Always have an apple from your jolly garden",0,925,1780,66,38,Gold,true);
        Label(content,"Dear player","Dear Player.",0,1010,1500,60,34,Gold,true);
        var footer=Label(textRoot.transform,"Ludum Dare credit","Made for Ludum Dare 60",0,0,600,30,18,Color.white,true);
        rect=footer.rectTransform;rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(24,18);footer.alignment=TextAlignmentOptions.BottomLeft;
    }
    private void Column(string name,string roles,float x)
    {Label(content,name,name,x,595,530,60,27,Color.white,true);Label(content,name+" roles",roles,x,685,530,200,27,Color.white,true);}
    private TMP_Text Label(Transform parent,string name,string value,float x,float top,float width,float height,float size,Color colour,bool bold)
    {
        var rect=Rect(parent,name,x,top,width,height);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font?font:TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=size;text.color=colour;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
        text.alignment=TextAlignmentOptions.Top;text.textWrappingMode=TextWrappingModes.Normal;text.raycastTarget=false;return text;
    }
    private static RectTransform Rect(Transform parent,string name,float x,float top,float width,float height)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,1);rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,-top);return rect;
    }
    private void OnDisable(){StopAllCoroutines();if(music)music.Stop();if(canvas)Destroy(canvas.gameObject);IsPlaying=false;}
}
