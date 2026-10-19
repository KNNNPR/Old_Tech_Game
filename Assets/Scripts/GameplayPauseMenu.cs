using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Esc settings during free play. Device exits keep priority over the settings key.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class GameplayPauseMenu:MonoBehaviour
{
    public static GameplayPauseMenu Instance {get;private set;}
    public bool IsOpen {get;private set;}
    public bool QuitRequested {get;private set;}
    private GameplayPauseAssets assets;
    private GameObject window;
    private Slider volume;
    private TMP_Text volumeLabel;
    private AudioSource feedback;
    private PlayerInteractor player;
    private FirstPersonController controller;
    private bool savedPlayerEnabled,savedInput,savedControllerEnabled,savedControl,savedCursorVisible,savedAudioPause;
    private CursorLockMode savedCursor;
    private float savedTimeScale;
    private static readonly Color Green=new Color(.49f,.95f,.63f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()=>Instance=null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {if(!Instance)new GameObject("Gameplay settings").AddComponent<GameplayPauseMenu>();}
    private void Awake()
    {
        if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
        assets=Resources.Load<GameplayPauseAssets>("GameplayPauseAssets");
        var sound=new GameObject("Settings feedback");sound.transform.SetParent(transform,false);feedback=sound.AddComponent<AudioSource>();feedback.playOnAwake=false;feedback.spatialBlend=1;feedback.minDistance=2;feedback.maxDistance=18;feedback.dopplerLevel=0;feedback.ignoreListenerPause=true;feedback.ignoreListenerVolume=false;
        SceneManager.sceneLoaded+=OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene,LoadSceneMode mode){if(IsOpen)Close(false);player=null;controller=null;}
    private void Update()
    {
        if(Keyboard.current==null||!Keyboard.current.escapeKey.wasPressedThisFrame)return;
        if(IsOpen)Close();else TryOpen();
    }
    public bool CanOpen
    {
        get
        {
            string scene=SceneManager.GetActiveScene().name;
            if(IsOpen||scene==GameFlow.MenuScene||scene=="GAME_OVER"||scene=="TITLES"||BlockSceneTransition.IsRunning)return false;
            if(!player)player=FindFirstObjectByType<PlayerInteractor>();
            if(!player||!player.enabled||player.IsFocused)return false;
            controller=player.Controller;
            if(!controller||!controller.isActiveAndEnabled||!controller.ControlEnabled)return false;
            if(player.HasExternalInteraction)
            {
                // DELIRIUM owns walking input even before choosing its irreversible close view.
                var ritual=FindFirstObjectByType<DeliriumSequence>();
                if(!ritual||ritual.IsLocked)return false;
            }
            else if(!player.InputEnabled)return false;
            return true;
        }
    }
    public bool TryOpen()
    {
        if(!CanOpen)return false;
        if(!window)Build();EnsureEventSystem();GameSettings.Load();
        savedPlayerEnabled=player.enabled;savedInput=player.InputEnabled;
        savedControllerEnabled=controller.enabled;savedControl=controller.ControlEnabled;
        savedCursor=Cursor.lockState;savedCursorVisible=Cursor.visible;savedTimeScale=Time.timeScale;savedAudioPause=AudioListener.pause;
        player.InputEnabled=false;player.enabled=false;controller.SetControlEnabled(false);controller.enabled=false;
        Time.timeScale=0;AudioListener.pause=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        IsOpen=true;window.SetActive(true);volume.SetValueWithoutNotify(GameSettings.Volume);UpdateVolume(GameSettings.Volume);
        if(player.PlayerCamera)feedback.transform.position=player.PlayerCamera.transform.position;
        Play(assets?assets.press:null);EventSystem.current?.SetSelectedGameObject(null);return true;
    }
    public void Close(bool playSound=true)
    {
        if(!IsOpen)return;IsOpen=false;window.SetActive(false);
        Time.timeScale=savedTimeScale;AudioListener.pause=savedAudioPause;
        if(player){player.InputEnabled=savedInput;player.enabled=savedPlayerEnabled;}
        if(controller){controller.enabled=savedControllerEnabled;controller.SetControlEnabled(savedControl);}
        Cursor.lockState=savedCursor;Cursor.visible=savedCursorVisible;
        if(playSound)Play(assets?assets.back:null);
    }
    public void BackToMenu(){Close(false);Play(assets?assets.press:null);GameFlow.BackToMenu();}
    public void Quit()
    {
        QuitRequested=true;Play(assets?assets.press:null);Close(false);
        StartCoroutine(QuitAfterFeedback());
    }
    private IEnumerator QuitAfterFeedback()
    {
        yield return new WaitForSecondsRealtime(.15f);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying=false;
#else
        Application.Quit();
#endif
    }
    private void UpdateVolume(float value){volumeLabel.text=Mathf.RoundToInt(value*100)+"%";}
    private void ChangeVolume(float value){GameSettings.SetVolume(value);UpdateVolume(value);}
    private void Play(AudioClip clip){if(clip){feedback.Stop();feedback.clip=clip;feedback.volume=.3f;feedback.Play();}}
    private static void EnsureEventSystem()
    {
        if(EventSystem.current)return;
        var root=new GameObject("Settings event system",typeof(EventSystem),typeof(InputSystemUIInputModule));
        root.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
    private void Build()
    {
        window=new GameObject("Helpy-style settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));window.transform.SetParent(transform,false);
        var canvas=window.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32700;
        var scaler=window.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.45f;
        var dim=Image(window.transform,"Backdrop",new Color(0,0,0,.72f));Stretch(dim.rectTransform);
        var panel=Rect(window.transform,"Dialogue panel",0,0,760,438);var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.002f,.016f,.009f,.98f);
        var shadow=panel.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(0,0,0,.65f);shadow.effectDistance=new Vector2(0,-5);
        Rule(panel,"Top phosphor rule",1,new Color(.24f,.85f,.4f,.5f));Rule(panel,"Bottom phosphor rule",0,new Color(.1f,.5f,.24f,.3f));
        var header=Label(panel,"SETTINGS",0,164,680,45,30);header.characterSpacing=3;header.alignment=TextAlignmentOptions.MidlineLeft;header.gameObject.AddComponent<RetroSubtitleText>();
        var name=Label(panel,"MASTER VOLUME",0,98,680,30,15);name.characterSpacing=2;name.color=new Color(.29f,.68f,.42f);name.alignment=TextAlignmentOptions.MidlineLeft;
        volumeLabel=Label(panel,"100%",274,98,130,30,16);volumeLabel.alignment=TextAlignmentOptions.MidlineRight;
        var rail=Rect(panel,"Volume",0,51,680,32);volume=rail.gameObject.AddComponent<Slider>();volume.minValue=0;volume.maxValue=1;volume.wholeNumbers=false;
        var track=Image(rail,"Track",new Color(.05f,.14f,.08f));track.rectTransform.anchorMin=new Vector2(0,.4f);track.rectTransform.anchorMax=new Vector2(1,.6f);track.rectTransform.offsetMin=track.rectTransform.offsetMax=Vector2.zero;
        var fillArea=Rect(rail,"Fill area",0,0,0,0);fillArea.anchorMin=new Vector2(0,.42f);fillArea.anchorMax=new Vector2(1,.58f);fillArea.offsetMax=new Vector2(-12,0);fillArea.offsetMin=new Vector2(12,0);
        var fill=Image(fillArea,"Fill",new Color(.18f,.64f,.32f));fill.rectTransform.anchorMin=new Vector2(0,.4f);fill.rectTransform.anchorMax=new Vector2(1,.6f);fill.rectTransform.offsetMin=fill.rectTransform.offsetMax=Vector2.zero;
        var handleArea=Rect(rail,"Handle area",0,0,0,0);Stretch(handleArea);handleArea.offsetMin=new Vector2(12,2);handleArea.offsetMax=new Vector2(-12,-2);
        var handle=Image(handleArea,"Handle",Green);handle.rectTransform.sizeDelta=new Vector2(12,0);volume.fillRect=fill.rectTransform;volume.handleRect=handle.rectTransform;volume.targetGraphic=handle;volume.direction=Slider.Direction.LeftToRight;
        volume.onValueChanged.AddListener(ChangeVolume);
        Button(panel,"RESUME",-18,CloseButton);Button(panel,"BACK TO MENU",-76,BackToMenu);Button(panel,"EXIT GAME",-134,Quit);
        var hint=Label(panel,"[ ESC ]  RESUME",0,-195,680,22,12);hint.characterSpacing=2;hint.color=new Color(.29f,.68f,.42f);hint.alignment=TextAlignmentOptions.MidlineRight;
        window.SetActive(false);
    }
    private void CloseButton()=>Close();
    private void Button(Transform parent,string name,float y,UnityEngine.Events.UnityAction callback)
    {
        var rect=Rect(parent,name,0,y,680,46);var image=rect.gameObject.AddComponent<Image>();image.color=Color.white;
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var colours=button.colors;
        colours.normalColor=new Color(.035f,.10f,.06f,1);colours.highlightedColor=new Color(.06f,.23f,.12f,1);colours.selectedColor=colours.highlightedColor;colours.pressedColor=new Color(.1f,.36f,.19f,1);colours.fadeDuration=.12f;button.colors=colours;button.onClick.AddListener(callback);
        var text=Label(rect,name,0,0,632,42,21);text.alignment=TextAlignmentOptions.MidlineLeft;
        var trigger=rect.gameObject.AddComponent<EventTrigger>();var hover=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};hover.callback.AddListener(_=>Play(assets?assets.hover:null));trigger.triggers.Add(hover);
    }
    private TMP_Text Label(Transform parent,string value,float x,float y,float width,float height,float size)
    {
        var rect=Rect(parent,value,x,y,width,height);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=assets&&assets.font?assets.font:TMP_Settings.defaultFontAsset;label.text=value;label.fontSize=size;label.color=Color.white;label.raycastTarget=false;
        label.enableVertexGradient=true;label.colorGradient=new VertexGradient(new Color(.7f,1f,.72f),new Color(.6f,1f,.68f),new Color(.16f,.74f,.34f),new Color(.22f,.85f,.4f));label.textWrappingMode=TextWrappingModes.NoWrap;return label;
    }
    private static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
    {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,y);return rect;}
    private static Image Image(Transform parent,string name,Color colour)
    {var rect=Rect(parent,name,0,0,0,0);var image=rect.gameObject.AddComponent<Image>();image.color=colour;return image;}
    private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
    private static void Rule(Transform parent,string name,float y,Color colour)
    {var image=Image(parent,name,colour);var rect=image.rectTransform;rect.anchorMin=new Vector2(0,y);rect.anchorMax=new Vector2(1,y);rect.sizeDelta=new Vector2(0,1);rect.anchoredPosition=Vector2.zero;image.raycastTarget=false;}
    private void OnDestroy()
    {if(Instance!=this)return;if(IsOpen)Close(false);SceneManager.sceneLoaded-=OnSceneLoaded;Instance=null;}
}
