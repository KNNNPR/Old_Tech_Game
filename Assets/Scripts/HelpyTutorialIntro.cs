using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Scene-local arrival, Helpy's authored animations and action-gated room tour.</summary>
[DefaultExecutionOrder(-100)]
public sealed class HelpyTutorialIntro : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController player;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Transform robot;
    [SerializeField, HideInInspector] private Vector3 faceNormal = Vector3.down;
    [SerializeField] private Canvas playerHUD;
    [SerializeField] private TMP_FontAsset subtitleFont;

    [Header("Arrival / TUTORIAL only")]
    [SerializeField] private Transform entranceStart;
    [SerializeField] private Transform[] entranceRoute;
    [SerializeField] private Transform roomCentre;
    [SerializeField] private Transform entranceDoor;
    [SerializeField] private Transform doorHinge;
    [SerializeField] private float doorOpenAngle = 90f;
    [SerializeField, Min(1f)] private float entryWalkSeconds = 5.2f;

    [Header("Helpy's authored clips")]
    [SerializeField] private AnimationClip appearance;
    [SerializeField] private AnimationClip[] movement;
    [SerializeField] private AnimationClip idle;
    [SerializeField, Range(.1f,2f)] private float movementSpeed = .65f;
    [SerializeField] private PunchCardTubeSpawner tube;
    [SerializeField] private ItemSlot cardReader;
    [SerializeField] private FeedLever cardLever;

    [Header("Camera")]
    [SerializeField, Min(.1f)] private float turnDuration = .5f;
    [SerializeField, Min(.1f)] private float approachDuration = 1.5f;
    [SerializeField, Min(.1f)] private float returnDuration = 1.25f;
    [SerializeField, Range(25,65)] private float closeupFov = 42f;

    [Header("Dialogue / Space advances one line")]
    [SerializeField] private bool autoAdvance = true;
    [SerializeField, Min(1)] private float minimumLineDuration = 4f;
    [SerializeField, Min(.01f)] private float secondsPerCharacter = .075f;
    [SerializeField, TextArea(2,4)] private string[] lines =
    {
        "Greetings partner!",
        "Call me Helpy and I'm here to help of course!",
        "Sometimes people get bad punch cards that are somehow <i><color=#B5FFA0>defective</color></i>.",
        "All you need to do is find <i><color=#B5FFA0>defective</color></i> punch cards.",
        "Let me show you!"
    };

    public bool IsPlaying { get; private set; }
    public bool HasFinished { get; private set; }
    public int CurrentLine { get; private set; } = -1;
    public enum TourStage { Entering, Appearing, Introduction, MovingToTube, Tube, WaitingForCard, MovingToReader, Reader, WaitingForFeed, ReaderComplete, MovingToRecorder, Recorder, MovingToObserver, Observer, Departing, Finished }
    public TourStage Stage { get; private set; }
    public bool IsPlayerLocked => playerLocked;

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float homeFov;
    private bool controllerWasEnabled, controlWasEnabled, interactionWasEnabled, hudWasEnabled;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private CanvasGroup subtitles;
    private TMP_Text dialogueText, lineNumber, continueHint;
    private bool advanceRequested;
    private bool playerLocked, tubeWasAutomatic, idlePlaying, cardInserted, cardFed;
    private CharacterController character;
    private bool characterWasEnabled;
    private Pickupable tutorialCard;
    private Animator[] animators;
    private bool[] animatorEnabled;
    private Vector3 idlePosition, doorClosedPosition;
    private Vector3 greetingPosition;
    private Quaternion idleRotation, doorClosedRotation;
    private float idleTime;
    private int spokenLines;
    private CanvasGroup entranceFade;

    private IEnumerator Start()
    {
        if (!playerCamera || !player || !interactor || !robot || !entranceStart || entranceRoute == null || entranceRoute.Length == 0 || !roomCentre || !entranceDoor || !doorHinge
            || !appearance || !idle || movement == null || movement.Length != 4 || !tube || !cardReader || !cardLever || lines == null || lines.Length == 0)
        {
            Debug.LogError("Helpy tour is missing its TUTORIAL scene references or authored clips.",this);
            yield break;
        }
        var face = FindFace();
        if (!face)
        {
            Debug.LogError("Helpy's face mesh could not be found.",this);
            yield break;
        }

        homePosition = playerCamera.transform.localPosition;
        homeRotation = playerCamera.transform.localRotation;
        homeFov = playerCamera.fieldOfView;
        controllerWasEnabled = player.enabled;
        controlWasEnabled = player.ControlEnabled;
        interactionWasEnabled = interactor.InputEnabled;
        hudWasEnabled = playerHUD && playerHUD.enabled;
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        IsPlaying = playerLocked = true;
        character = player.GetComponent<CharacterController>();
        characterWasEnabled = character && character.enabled;
        if (character) character.enabled = false;
        player.SetControlEnabled(false);
        // Disabling also suspends head bob and gravity while the camera is away from the player.
        player.enabled = false;
        interactor.InputEnabled = false;
        if (playerHUD) playerHUD.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BuildSubtitles();

        tubeWasAutomatic = tube.AutomaticSpawning;
        tube.AutomaticSpawning = false;
        cardReader.ItemInserted += OnCardInserted;
        cardLever.CardFed += OnCardFed;
        animators = robot.GetComponentsInChildren<Animator>(true);
        animatorEnabled = new bool[animators.Length];
        for (int i=0;i<animators.Length;i++) { animatorEnabled[i]=animators[i].enabled;animators[i].enabled=false; }
        var oldSway = GetComponent<HelpyIdleSway>();
        if (oldSway) oldSway.enabled=false;
        doorClosedPosition=entranceDoor.position;doorClosedRotation=entranceDoor.rotation;
        appearance.SampleAnimation(robot.gameObject,appearance.length);
        greetingPosition=face.bounds.center;
        appearance.SampleAnimation(robot.gameObject,0);
        yield return EnterRoom();
        Stage=TourStage.Appearing;
        yield return PlayRobot(appearance,1f);
        StartIdle();

        Vector3 centre = face.bounds.center;
        Vector3 normal = face.transform.TransformDirection(faceNormal).normalized;
        float height = Mathf.Max(face.bounds.size.y,.3f);
        float distance = height / (2f * Mathf.Tan(closeupFov * Mathf.Deg2Rad * .5f) * .58f);
        Vector3 cameraPosition = centre + normal * distance + Vector3.up * height * .04f;
        Vector3 aim = centre - Vector3.up * height * .12f;
        Quaternion closeupRotation = Quaternion.LookRotation(aim-cameraPosition,Vector3.up);
        Quaternion turnRotation = Quaternion.LookRotation(centre-playerCamera.transform.position,Vector3.up);
        yield return MoveCamera(playerCamera.transform.position,turnRotation,homeFov,turnDuration);
        Vector3? approachControl = null, faceControl = null;
        if (Vector3.Dot(playerCamera.transform.position-centre,normal) < height*.2f)
        {
            // A spawn behind the monitor needs an arc over its top instead of crossing the glass.
            approachControl = playerCamera.transform.position + Vector3.up * height * 1.2f;
            faceControl = cameraPosition + Vector3.up * height * .55f;
        }
        yield return MoveCamera(cameraPosition,closeupRotation,closeupFov,approachDuration,approachControl,faceControl);

        Stage=TourStage.Introduction;
        foreach (var line in lines) yield return Say(line);
        CurrentLine = -1;
        subtitles.alpha = 0;
        Transform parent = playerCamera.transform.parent;
        Vector3 returnPosition = parent ? parent.TransformPoint(homePosition) : homePosition;
        Quaternion returnRotation = parent ? parent.rotation * homeRotation : homeRotation;
        yield return MoveCamera(returnPosition,returnRotation,homeFov,returnDuration,faceControl,approachControl);
        RestorePlayer();

        Stage=TourStage.MovingToTube;yield return PlayRobot(movement[0],movementSpeed);StartIdle();
        Stage=TourStage.Tube;
        if (!tube.LastSpawnedCard) tube.TrySpawnNext();
        yield return Say("From this tube you get your <i><color=#B5FFA0>precious punch card</color></i>!");
        yield return Say("Grab it carefully.");
        Stage=TourStage.WaitingForCard;
        Objective("Pick up a punch card from the tube.","[ E ]  PICK UP CARD");
        while (!interactor.HeldItem || !interactor.HeldItem.GetComponent<PunchCard>()) yield return null;
        tutorialCard=interactor.HeldItem;
        subtitles.alpha=0;

        Stage=TourStage.MovingToReader;yield return PlayRobot(movement[1],movementSpeed);StartIdle();
        Stage=TourStage.Reader;
        yield return Say("Here you insert your punch card and inspect it.");
        Stage=TourStage.WaitingForFeed;
        Objective("Insert your punch card into the reader, then pull the lever.","[ E ]  INSERT / PULL LEVER");
        while (!cardInserted || !cardFed) yield return null;
        Stage=TourStage.ReaderComplete;
        yield return Say("Great! Now you need to analyze it and throw it away if it's <i><color=#B5FFA0>defective</color></i>...");

        subtitles.alpha=0;
        Stage=TourStage.MovingToRecorder;yield return PlayRobot(movement[2],movementSpeed);StartIdle();
        Stage=TourStage.Recorder;
        yield return Say("Before you analyze it, let's see what else you've got here.");
        yield return Say("Here you <i><color=#B5FFA0>listen</color></i> to your card.");

        subtitles.alpha=0;
        Stage=TourStage.MovingToObserver;yield return PlayRobot(movement[3],movementSpeed);StartIdle();
        Stage=TourStage.Observer;
        yield return Say("And here you <i><color=#B5FFA0>observe</color></i> your punch card!");
        yield return Say("Okay, hope you won't need to go outside! Goodbye!");
        subtitles.alpha=0;CurrentLine=-1;
        Stage=TourStage.Departing;
        yield return PlayRobot(appearance,-1f,true);
        robot.gameObject.SetActive(false);
        tube.AutomaticSpawning=tubeWasAutomatic;
        Unsubscribe();
        Stage=TourStage.Finished;IsPlaying=false;
        HasFinished = true;
    }

    private IEnumerator EnterRoom()
    {
        Stage=TourStage.Entering;
        player.transform.SetPositionAndRotation(entranceStart.position,entranceStart.rotation);
        playerCamera.transform.localPosition=homePosition;playerCamera.transform.localRotation=homeRotation;
        BuildEntranceFade();
        for(float t=0;t<.6f;t+=Time.unscaledDeltaTime) { entranceFade.alpha=1-t/.6f;yield return null; }
        entranceFade.alpha=0;
        yield return SwingDoor(0,doorOpenAngle,.8f);
        var points=new Vector3[entranceRoute.Length+2];
        points[0]=player.transform.position;
        for(int i=0;i<entranceRoute.Length;i++)points[i+1]=entranceRoute[i].position;
        points[points.Length-1]=roomCentre.position;
        var lengths=new float[points.Length];
        for(int i=1;i<points.Length;i++)lengths[i]=lengths[i-1]+Vector3.Distance(points[i-1],points[i]);
        float length=lengths[lengths.Length-1];
        Quaternion viewRotation=playerCamera.transform.rotation;
        for(float t=0;t<entryWalkSeconds;t+=Time.unscaledDeltaTime)
        {
            float p=Mathf.SmoothStep(0,1,t/entryWalkSeconds);
            float distance=p*length;
            int segment=0;
            while(segment<points.Length-2 && distance>lengths[segment+1])segment++;
            float along=Mathf.InverseLerp(lengths[segment],lengths[segment+1],distance);
            player.transform.position=Vector3.Lerp(points[segment],points[segment+1],along);
            Vector3 forward=points[segment+1]-points[segment];forward.y=0;
            Quaternion walkingRotation=Quaternion.LookRotation(forward,Vector3.up);
            player.transform.rotation=Quaternion.Slerp(player.transform.rotation,walkingRotation,1f-Mathf.Exp(-Time.unscaledDeltaTime*6f));
            playerCamera.transform.localPosition=homePosition+Vector3.up*(Mathf.Sin(t*11f)*.025f*Mathf.Sin(p*Mathf.PI));
            Quaternion meetingRotation=Quaternion.LookRotation(greetingPosition-playerCamera.transform.position,Vector3.up);
            float facingHelpy=Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.55f,.9f,p));
            Quaternion desired=Quaternion.Slerp(walkingRotation,meetingRotation,facingHelpy);
            viewRotation=Quaternion.Slerp(viewRotation,desired,1f-Mathf.Exp(-Time.unscaledDeltaTime*6f));
            playerCamera.transform.rotation=viewRotation;
            yield return null;
        }
        player.transform.position=roomCentre.position;playerCamera.transform.localPosition=homePosition;
        playerCamera.transform.localRotation=homeRotation;
        player.SetLookDirection(greetingPosition-playerCamera.transform.position);
        yield return SwingDoor(doorOpenAngle,0,.7f);
    }
    private IEnumerator SwingDoor(float from,float to,float seconds)
    {
        for(float t=0;t<seconds;t+=Time.unscaledDeltaTime)
        {
            Quaternion turn=Quaternion.AngleAxis(Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/seconds)),Vector3.up);
            entranceDoor.SetPositionAndRotation(doorHinge.position+turn*(doorClosedPosition-doorHinge.position),turn*doorClosedRotation);
            yield return null;
        }
        Quaternion end=Quaternion.AngleAxis(to,Vector3.up);
        entranceDoor.SetPositionAndRotation(doorHinge.position+end*(doorClosedPosition-doorHinge.position),end*doorClosedRotation);
    }
    private IEnumerator PlayRobot(AnimationClip clip,float speed,bool fromCurrentPose=false)
    {
        idlePlaying=false;
        Vector3 position=robot.localPosition;
        Quaternion rotation=robot.localRotation;
        float start=speed<0f?clip.length:0f;
        clip.SampleAnimation(robot.gameObject,start);
        Vector3 clipOrigin=robot.localPosition;
        Quaternion clipRotation=robot.localRotation;
        float duration=clip.length/Mathf.Max(.1f,Mathf.Abs(speed));
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime)
        {
            Sample(Mathf.Clamp(start+t*speed,0f,clip.length));
            yield return null;
        }
        Sample(speed<0f?0f:clip.length);

        void Sample(float time)
        {
            clip.SampleAnimation(robot.gameObject,time);
            if(fromCurrentPose)
            {
                // The appearance clip contains absolute coordinates at the first stop.
                // Use its motion relative to the goodbye pose so Helpy lifts off where he is standing.
                robot.SetLocalPositionAndRotation(position+robot.localPosition-clipOrigin,
                    rotation*Quaternion.Inverse(clipRotation)*robot.localRotation);
            }
        }
    }
    private void StartIdle()
    {
        idlePosition=robot.localPosition;idleRotation=robot.localRotation;
        idleTime=0;idlePlaying=true;
    }
    private void LateUpdate()
    {
        if (!idlePlaying || !robot || !idle) return;
        idleTime+=Time.unscaledDeltaTime;
        idle.SampleAnimation(robot.gameObject,Mathf.Repeat(idleTime,Mathf.Max(.01f,idle.length)));
        // move_robot also contains the original root position; keep the current route stop instead.
        robot.SetLocalPositionAndRotation(idlePosition,idleRotation);
    }
    private IEnumerator Say(string line)
    {
        subtitles.alpha=1;CurrentLine=spokenLines++;
        dialogueText.text=line;dialogueText.maxVisibleCharacters=0;dialogueText.ForceMeshUpdate();
        lineNumber.text=$"{spokenLines:00} / {lines.Length+8:00}";
        continueHint.text="[ SPACE ]  NEXT LINE";
        int count=dialogueText.textInfo.characterCount;
        float elapsed=0,typedSeconds=count*secondsPerCharacter;
        float hold=Mathf.Max(minimumLineDuration,count*.045f);
        advanceRequested=false;
        yield return null;
        while(!advanceRequested && !(Keyboard.current?.spaceKey.wasPressedThisFrame??false) && (!autoAdvance || elapsed<typedSeconds+hold))
        {
            elapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
            dialogueText.maxVisibleCharacters=Mathf.Min(count,(int)(elapsed/secondsPerCharacter));
            yield return null;
        }
        CurrentLine=-1;
    }
    private void Objective(string line,string hint)
    {
        CurrentLine=-1;dialogueText.text=line;dialogueText.maxVisibleCharacters=int.MaxValue;
        lineNumber.text="TASK";continueHint.text=hint;subtitles.alpha=1;
    }
    private void OnCardInserted(Pickupable card) { if(card==tutorialCard) cardInserted=true; }
    private void OnCardFed(Pickupable card) { if(card==tutorialCard) { cardInserted=true;cardFed=true; } }
    private void Unsubscribe()
    {
        if(cardReader)cardReader.ItemInserted-=OnCardInserted;
        if(cardLever)cardLever.CardFed-=OnCardFed;
    }

    /// <summary>Also usable by an on-screen accessibility button; never skips the whole introduction.</summary>
    public void AdvanceLine()
    {
        if (IsPlaying && CurrentLine >= 0) advanceRequested = true;
    }

    private Renderer FindFace()
    {
        foreach (var mesh in robot.GetComponentsInChildren<Renderer>(true))
            if (mesh.name == "Cube.001") return mesh;
        return null;
    }

    private IEnumerator MoveCamera(Vector3 destination,Quaternion rotation,float fov,float duration,Vector3? firstControl=null,Vector3? secondControl=null)
    {
        Vector3 from = playerCamera.transform.position;
        Quaternion fromRotation = playerCamera.transform.rotation;
        float fromFov = playerCamera.fieldOfView;
        for (float elapsed=0;elapsed<duration;elapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f))
        {
            float t = Mathf.SmoothStep(0,1,elapsed/duration);
            float u = 1-t;
            Vector3 position = firstControl.HasValue && secondControl.HasValue
                ? u*u*u*from + 3*u*u*t*firstControl.Value + 3*u*t*t*secondControl.Value + t*t*t*destination
                : Vector3.Lerp(from,destination,t);
            playerCamera.transform.SetPositionAndRotation(position,Quaternion.Slerp(fromRotation,rotation,t));
            playerCamera.fieldOfView = Mathf.Lerp(fromFov,fov,t);
            yield return null;
        }
        playerCamera.transform.SetPositionAndRotation(destination,rotation);
        playerCamera.fieldOfView = fov;
    }

    private void RestorePlayer()
    {
        if (!playerLocked) return;
        playerLocked = false;
        CurrentLine = -1;
        if (playerCamera)
        {
            playerCamera.transform.localPosition = homePosition;
            playerCamera.transform.localRotation = homeRotation;
            playerCamera.fieldOfView = homeFov;
        }
        if (player) { player.SetControlEnabled(controlWasEnabled); player.enabled = controllerWasEnabled; }
        if (character) character.enabled=characterWasEnabled;
        if (interactor) interactor.InputEnabled = interactionWasEnabled;
        if (playerHUD) playerHUD.enabled = hudWasEnabled;
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        idlePlaying=false;
        RestorePlayer();
        Unsubscribe();
        if (tube && IsPlaying) tube.AutomaticSpawning=tubeWasAutomatic;
        if (animators!=null) for(int i=0;i<animators.Length;i++) if(animators[i])animators[i].enabled=animatorEnabled[i];
        if(entranceDoor && IsPlaying)entranceDoor.SetPositionAndRotation(doorClosedPosition,doorClosedRotation);
        IsPlaying=false;
        if (subtitles) Destroy(subtitles.gameObject);
        if (entranceFade) Destroy(entranceFade.gameObject);
    }

    private void BuildSubtitles()
    {
        var root = new GameObject("Helpy subtitles",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(CanvasGroup));
        root.transform.SetParent(transform,false);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280,720);
        scaler.matchWidthOrHeight = .45f;
        subtitles = root.GetComponent<CanvasGroup>();
        subtitles.alpha = 0;
        subtitles.blocksRaycasts = subtitles.interactable = false;
        var panel = Rect("Dialogue panel",root.transform,new Vector2(.1f,0),new Vector2(.9f,0),new Vector2(0,30),new Vector2(0,154));
        panel.pivot = new Vector2(.5f,0);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(.002f,.016f,.009f,.96f);
        background.raycastTarget = false;
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.65f);shadow.effectDistance = new Vector2(0,-5);
        Rule(panel,"Top phosphor rule",1,new Color(.24f,.85f,.4f,.5f));
        Rule(panel,"Bottom phosphor rule",0,new Color(.1f,.5f,.24f,.3f));
        var speaker = Text(panel,"Speaker",13,new Vector2(26,-22),new Vector2(600,22),new Vector2(0,1));
        speaker.text = "HELPY  /  FIELD ASSISTANT";speaker.characterSpacing = 3;
        speaker.color = new Color(.49f,.95f,.63f);
        lineNumber = Text(panel,"Line",12,new Vector2(-26,-22),new Vector2(110,22),Vector2.one);
        lineNumber.alignment = TextAlignmentOptions.Right;lineNumber.color = new Color(.25f,.65f,.4f);
        dialogueText = Text(panel,"Dialogue",27,Vector2.zero,Vector2.zero,new Vector2(.5f,.5f));
        var body = dialogueText.rectTransform;body.anchorMin = Vector2.zero;body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(26,36);body.offsetMax = new Vector2(-26,-44);
        dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
        dialogueText.enableVertexGradient = true;
        dialogueText.colorGradient = new VertexGradient(new Color(.7f,1f,.72f),new Color(.6f,1f,.68f),new Color(.16f,.74f,.34f),new Color(.22f,.85f,.4f));
        dialogueText.gameObject.AddComponent<RetroSubtitleText>();
        var hint = Text(panel,"Continue hint",12,new Vector2(-26,14),new Vector2(550,20),Vector2.right);
        hint.rectTransform.pivot = Vector2.right;hint.alignment = TextAlignmentOptions.Right;
        hint.text = "[ SPACE ]  NEXT LINE";hint.characterSpacing = 2;hint.color = new Color(.29f,.68f,.42f);
        continueHint=hint;
    }

    private void BuildEntranceFade()
    {
        var root=new GameObject("Tutorial entrance fade",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
        root.transform.SetParent(transform,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=600;
        entranceFade=root.GetComponent<CanvasGroup>();entranceFade.blocksRaycasts=false;entranceFade.interactable=false;
        var cover=Rect("Black",root.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<Image>();
        cover.color=Color.black;cover.raycastTarget=false;
    }

    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.anchoredPosition=position;rect.sizeDelta=size;
        return rect;
    }
    private TMP_Text Text(Transform parent,string name,float size,Vector2 position,Vector2 dimensions,Vector2 anchor)
    {
        var rect = Rect(name,parent,anchor,anchor,position,dimensions);rect.pivot=anchor;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = subtitleFont ? subtitleFont : TMP_Settings.defaultFontAsset;
        text.fontSize = size;text.fontStyle = FontStyles.Normal;text.color=Color.white;text.raycastTarget=false;
        text.textWrappingMode=TextWrappingModes.Normal;
        return text;
    }
    private static void Rule(RectTransform panel,string name,float y,Color color)
    {
        var rule = Rect(name,panel,new Vector2(0,y),new Vector2(1,y),Vector2.zero,new Vector2(0,1));
        var image = rule.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;
    }
}
