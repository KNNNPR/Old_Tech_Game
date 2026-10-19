using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Scene-local first-person cigarette break. Ends on black, ready for a later story transition.</summary>
[DefaultExecutionOrder(-1000), DisallowMultipleComponent]
public sealed class IntermissionCinematic : MonoBehaviour
{
    public enum Shot { Opening, TurningRight, Elevator, FinalDrag, Ashtray, Walking, Fading, Finished, Screams, Collapse, Recoiling, Panic, Running, LookingUp }
    [Header("Scene")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Behaviour[] gameplayToSuspend;
    [SerializeField] private Canvas playerHUD;
    [SerializeField] private Transform openingPose, elevatorFocus, ashtrayFocus, cigaretteRest, walkCorner, doorStop, doorFocus;
    [Header("Cigarette")]
    [SerializeField] private Transform cigarette;
    [SerializeField] private ParticleSystem cigaretteSmoke;
    [SerializeField] private Renderer ember;
    [SerializeField] private Light emberLight;
    [SerializeField] private Vector3 heldPosition = new Vector3(.23f, -.18f, .54f);
    [SerializeField] private Vector3 heldEuler = new Vector3(-20, -24, -12);
    [Header("Player sounds")]
    [SerializeField] private AudioSource inhaleAudio, exhaleAudio, headTurnAudio;
    [SerializeField] private AudioSource windAudio;
    [Header("Pacing")]
    [SerializeField, Min(.1f)] private float rightTurnSeconds = 1.6f;
    [SerializeField, Min(.1f)] private float walkSpeed = .85f;
    [SerializeField, Min(.1f)] private float fadeSeconds = 1.5f;
    [SerializeField] private TMP_FontAsset subtitleFont;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    [Header("Second Intermission / red sky")]
    [SerializeField] private bool secondIntermission;
    [SerializeField] private IntermissionBuildingCollapse buildingCollapse;
    [SerializeField] private AudioSource streetAudio;
    [SerializeField] private AudioClip streetScreamClip;
    [SerializeField] private AudioSource streetMaleAudio;
    [SerializeField] private AudioSource collapseMusic;
    [SerializeField, Min(.5f)] private float runSpeed = 2.8f;
    [SerializeField, TextArea(2,4)] private string[] secondLines =
    {
        "What the hell is this... Why is it happening to me?",
        "I was supposed to just sort stupid <b>punchcards</b>, not to interfere with the <i>apocalypse</i>...",
        "Maybe just a few more days... Atleast they've got some spare apples to eat",
        "This <color=#FF3838>red</color> sky. <i>Posseses me more and more...</i>",
        "[Screams]",
        "<b>WHAT THE HELL?!!</b>",
        "I NEED TO GET BACK.",
        "MAYBE THESE PUNCHCARDS",
        "<b>MAYBE THEY WILL ANSWER ME</b>"
    };

    private static readonly string[] Lines =
    {
        "Weird day",
        "Hope I'll be home next day",
        "!",
        "Elevator closed?",
        "It wasn't a few hours ago!",
        "Darn it. I need to ask Helpy about it."
    };
    public Shot CurrentShot { get; private set; }
    public int CurrentLine { get; private set; } = -1;
    public bool HasFinished { get; private set; }
    public bool CigarettePlaced { get; private set; }
    public bool CigaretteDropped { get; private set; }
    public float FadeAmount { get; private set; }

    private Vector3 desiredPosition, savedCameraPosition, savedCigarettePosition;
    private Quaternion desiredRotation, savedCameraRotation, savedCigaretteRotation;
    private Vector3 savedCigaretteScale;
    private float savedFov, drag, heat = .5f, walking;
    private bool inHand = true, initialized, advance, savedHUD;
    private bool[] savedBehaviours;
    private CursorLockMode savedCursor;
    private bool savedCursorVisible;
    private Canvas ui;
    private CanvasGroup subtitleGroup;
    private TMP_Text subtitle, lineNumber;
    private RetroSubtitleText subtitleMotion;
    private Image fade;
    private MaterialPropertyBlock emberProperties;
    private float panic;
    private bool savedCigaretteActive;
    private Rigidbody droppedCigaretteBody;
    private CapsuleCollider droppedCigaretteCollider;
    private float cigaretteThrow;
    private bool takingDrag;
    private float windVolume;
    private float collapseMusicGain;

    private void Awake()
    {
        if (!playerCamera || !openingPose || !elevatorFocus || !ashtrayFocus || !cigaretteRest || !cigarette || !doorStop || !doorFocus)
        {
            Debug.LogError("Intermission cinematic needs its scene camera, props and shot markers.", this);
            enabled = false;
            return;
        }
        if (secondIntermission && (!buildingCollapse || secondLines == null || secondLines.Length != 9))
        {
            Debug.LogError("Second Intermission needs its building collapse and nine monologue lines.",this);
            enabled = false;
            return;
        }
        savedCameraPosition = playerCamera.transform.position;
        savedCameraRotation = playerCamera.transform.rotation;
        savedFov = playerCamera.fieldOfView;
        savedCigarettePosition = cigarette.position;
        savedCigaretteRotation = cigarette.rotation;
        savedCigaretteScale = cigarette.localScale;
        savedCigaretteActive = cigarette.gameObject.activeSelf;
        savedBehaviours = new bool[gameplayToSuspend.Length];
        for (int i = 0; i < gameplayToSuspend.Length; i++)
        {
            var behaviour = gameplayToSuspend[i];
            if (!behaviour) continue;
            savedBehaviours[i] = behaviour.enabled;
            behaviour.enabled = false;
        }
        savedHUD = playerHUD && playerHUD.enabled;
        if (playerHUD) playerHUD.enabled = false;
        savedCursor = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        desiredPosition = openingPose.position;
        desiredRotation = openingPose.rotation;
        playerCamera.transform.SetPositionAndRotation(desiredPosition, desiredRotation);
        playerCamera.fieldOfView = 58;
        emberProperties = new MaterialPropertyBlock();
        if (windAudio) windVolume = windAudio.volume;
        BuildUI();
        SetFade(1);
        initialized = true;
    }

    private IEnumerator Start()
    {
        if (!initialized) yield break;
        if (windAudio && windAudio.clip) { windAudio.loop = true; windAudio.Play(); }
        if (secondIntermission) { yield return SecondBreak(); yield break; }
        if (cigaretteSmoke) cigaretteSmoke.Play();
        CurrentShot = Shot.Opening;
        yield return FadeTo(0, .8f);
        StartCoroutine(FirstDrag());
        yield return Say(0, 5.5f);
        yield return Say(1, 8f);

        CurrentShot = Shot.TurningRight;
        yield return TurnRight(elevatorFocus.position, rightTurnSeconds);
        CurrentShot = Shot.Elevator;
        yield return Say(2, 2.2f);
        yield return Say(3, 5.5f);
        yield return Say(4, 7.5f);
        yield return Say(5, 10f);

        CurrentShot = Shot.FinalDrag;
        yield return TakeDrag();
        yield return Pause(.35f);
        CurrentShot = Shot.Ashtray;
        Quaternion down = Quaternion.LookRotation(ashtrayFocus.position - desiredPosition, Vector3.up);
        yield return MoveCamera(desiredPosition + openingPose.forward * .12f, down, .95f);
        inHand = false;
        Vector3 from = cigarette.position;
        Quaternion rotation = cigarette.rotation;
        for (float elapsed = 0; elapsed < 1.8f; elapsed += Step)
        {
            float t = Ease(elapsed / 1.8f), u = 1 - t;
            cigarette.position = u * u * u * from + 3 * u * u * t * (from + Vector3.up * .1f)
                + 3 * u * t * t * (cigaretteRest.position + Vector3.up * .18f) + t * t * t * cigaretteRest.position;
            cigarette.rotation = Quaternion.Slerp(rotation, cigaretteRest.rotation, t);
            cigarette.localScale = Vector3.Scale(savedCigaretteScale, new Vector3(1, 1, Mathf.Lerp(1, .62f, t)));
            yield return null;
        }
        cigarette.SetPositionAndRotation(cigaretteRest.position, cigaretteRest.rotation);
        CigarettePlaced = true;
        if (cigaretteSmoke) cigaretteSmoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        for (float elapsed = 0; elapsed < .7f; elapsed += Step)
        {
            heat = Mathf.Lerp(.5f, 0, elapsed / .7f);
            yield return null;
        }
        heat = 0;
        yield return Pause(.5f);
        Quaternion towardDoor = Quaternion.LookRotation(doorFocus.position - desiredPosition, Vector3.up);
        yield return MoveCamera(desiredPosition, towardDoor, 1.1f);

        CurrentShot = Shot.Walking;
        Vector3 start = desiredPosition;
        Vector3 corner = walkCorner ? walkCorner.position : Vector3.Lerp(start, doorStop.position, .5f);
        float seconds = (Vector3.Distance(start, corner) + Vector3.Distance(corner, doorStop.position)) / walkSpeed;
        GameplaySceneAudio.For(this)?.SetScriptedWalking(true, playerCamera.transform);
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            float t = Ease(elapsed / seconds), u = 1 - t;
            desiredPosition = u * u * start + 2 * u * t * corner + t * t * doorStop.position;
            desiredRotation = Quaternion.LookRotation(doorFocus.position - desiredPosition, Vector3.up);
            walking = Mathf.Sin(t * Mathf.PI);
            yield return null;
        }
        walking = 0;
        desiredPosition = doorStop.position;
        desiredRotation = Quaternion.LookRotation(doorFocus.position - desiredPosition, Vector3.up);
        GameplaySceneAudio.For(this)?.SetScriptedWalking(false);
        yield return Pause(.25f);
        CurrentShot = Shot.Fading;
        var doorSound = GameplaySceneAudio.For(this)?.PlayEntranceDoor(true);
        yield return FadeTo(1, fadeSeconds);
        if (doorSound != null) yield return doorSound;
        if (windAudio) windAudio.Stop();
        CurrentShot = Shot.Finished;
        HasFinished = true;
        onFinished.Invoke();
    }

    private IEnumerator SecondBreak()
    {
        if (cigaretteSmoke) cigaretteSmoke.Play();
        CurrentShot = Shot.Opening;
        yield return FadeTo(0,.8f);
        StartCoroutine(FirstDrag());
        yield return Say(0,8f);
        yield return Say(1,12f);
        yield return Say(2,10f);
        yield return TakeDrag();
        CurrentShot = Shot.LookingUp;
        Vector3 skyDirection=Vector3.ProjectOnPlane(openingPose.forward,Vector3.up).normalized+Vector3.up*.9f;
        var skyTurn=StartCoroutine(MoveCamera(desiredPosition,Quaternion.LookRotation(skyDirection,Vector3.up),1.1f));
        yield return Say(3,7f);
        yield return skyTurn;

        CurrentShot = Shot.Screams;
        if(collapseMusic&&collapseMusic.clip){collapseMusicGain=.055f;collapseMusic.volume=collapseMusicGain;collapseMusic.loop=true;collapseMusic.Play();}
        if (streetAudio && streetScreamClip) streetAudio.PlayOneShot(streetScreamClip,.7f);
        if (streetMaleAudio && streetMaleAudio.clip) StartCoroutine(DistantMaleScream());
        yield return Say(4,2.8f);
        CurrentShot = Shot.Collapse;
        // Optical zoom: turn and narrow the lens while staying at the balcony's original viewpoint.
        yield return MoveCamera(desiredPosition,Quaternion.LookRotation(buildingCollapse.TrackingPoint-desiredPosition,Vector3.up),1.1f,34);
        buildingCollapse.BeginCollapse();
        panic = .3f;
        while (!buildingCollapse.HasLanded)
        {
            desiredRotation = Quaternion.Slerp(desiredRotation,Quaternion.LookRotation(buildingCollapse.TrackingPoint-desiredPosition,Vector3.up),1-Mathf.Exp(-Step*9));
            panic = Mathf.Lerp(.3f,.85f,buildingCollapse.Progress);
            collapseMusicGain=Mathf.Lerp(.09f,.58f,Mathf.SmoothStep(0,1,buildingCollapse.Progress));
            yield return null;
        }
        yield return Pause(.6f);
        yield return Say(5,3.8f);

        CurrentShot = Shot.Recoiling;
        StartCoroutine(ThrowCigarette());
        Vector3 back = Vector3.ProjectOnPlane(openingPose.forward,Vector3.up).normalized;
        yield return MoveCamera(openingPose.position-back*.75f-Vector3.up*.08f,openingPose.rotation,.7f,63);
        CurrentShot = Shot.Panic;
        yield return Say(6,4.5f);
        yield return Say(7,4.5f);
        yield return Say(8,6f);

        CurrentShot = Shot.Running;
        yield return MoveCamera(desiredPosition,Quaternion.LookRotation(doorFocus.position-desiredPosition,Vector3.up),.38f,66);
        Vector3 start = desiredPosition;
        Vector3 corner = walkCorner ? walkCorner.position : Vector3.Lerp(start,doorStop.position,.5f);
        float seconds = Mathf.Max(1,(Vector3.Distance(start,corner)+Vector3.Distance(corner,doorStop.position))/runSpeed);
        GameplaySceneAudio.For(this)?.SetScriptedWalking(true, playerCamera.transform);
        for (float elapsed=0;elapsed<seconds;elapsed+=Step)
        {
            float t=Ease(elapsed/seconds),u=1-t;
            desiredPosition=u*u*start+2*u*t*corner+t*t*doorStop.position;
            desiredRotation=Quaternion.LookRotation(doorFocus.position-desiredPosition,Vector3.up);
            walking=Mathf.Sin(t*Mathf.PI); yield return null;
        }
        walking=0; desiredPosition=doorStop.position;
        desiredRotation=Quaternion.LookRotation(doorFocus.position-desiredPosition,Vector3.up);
        GameplaySceneAudio.For(this)?.SetScriptedWalking(false);
        CurrentShot=Shot.Fading;
        var doorSound = GameplaySceneAudio.For(this)?.PlayEntranceDoor(true);
        yield return FadeTo(1,fadeSeconds);
        if (doorSound != null) yield return doorSound;
        if (windAudio) windAudio.Stop();
        panic=0; CurrentShot=Shot.Finished; HasFinished=true;
        onFinished.Invoke();
    }

    private IEnumerator DistantMaleScream()
    {
        yield return new WaitForSecondsRealtime(.65f);
        if (streetMaleAudio && streetMaleAudio.clip) streetMaleAudio.PlayOneShot(streetMaleAudio.clip);
    }

    private IEnumerator ThrowCigarette()
    {
        for(float elapsed=0;elapsed<.14f;elapsed+=Step)
        {
            cigaretteThrow=Ease(elapsed/.14f);
            yield return null;
        }
        inHand=false; CigaretteDropped=true;
        // Fit the collider to the visible mesh, whose centre can differ from the prop's pivot.
        Bounds bounds=CigaretteLocalBounds();
        Vector3 size=bounds.size;
        int axis=size.x>size.y?(size.x>size.z?0:2):(size.y>size.z?1:2);
        float radius=Mathf.Max(.002f,Mathf.Max(size[(axis+1)%3],size[(axis+2)%3])*.5f);
        droppedCigaretteCollider=cigarette.gameObject.AddComponent<CapsuleCollider>();
        droppedCigaretteCollider.center=bounds.center;
        droppedCigaretteCollider.direction=axis;
        droppedCigaretteCollider.radius=radius;
        droppedCigaretteCollider.height=Mathf.Max(size[axis],radius*2);
        droppedCigaretteCollider.contactOffset=.001f;
        // The released prop must not land on the invisible player capsule.
        foreach(var behaviour in gameplayToSuspend)
            if(behaviour&&(behaviour is FirstPersonController||behaviour is PlayerInteractor))
                foreach(var collider in behaviour.GetComponentsInChildren<Collider>(true))
                    Physics.IgnoreCollision(droppedCigaretteCollider,collider,true);
        droppedCigaretteBody=cigarette.gameObject.AddComponent<Rigidbody>();
        droppedCigaretteBody.mass=.005f;
        droppedCigaretteBody.useGravity=true;
        droppedCigaretteBody.interpolation=RigidbodyInterpolation.Interpolate;
        droppedCigaretteBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        droppedCigaretteBody.linearDamping=.025f;
        droppedCigaretteBody.angularDamping=.12f;
        droppedCigaretteBody.maxAngularVelocity=20;
        Vector3 forward=Vector3.ProjectOnPlane(openingPose.forward,Vector3.up).normalized;
        droppedCigaretteBody.linearVelocity=forward*7+openingPose.right*.18f+Vector3.up*2.8f;
        droppedCigaretteBody.angularVelocity=openingPose.right*7+forward*4;
        yield return Pause(.9f);
        heat=.08f;
        if(cigaretteSmoke)cigaretteSmoke.Stop(true,ParticleSystemStopBehavior.StopEmitting);
        while(cigarette&&droppedCigaretteBody&&!droppedCigaretteBody.IsSleeping())
        {
            if(cigarette.position.y<openingPose.position.y-40){cigarette.gameObject.SetActive(false);yield break;}
            yield return null;
        }
    }

    private Bounds CigaretteLocalBounds()
    {
        Bounds result=new Bounds(Vector3.zero,new Vector3(.014f,.014f,.14f));
        bool found=false;
        foreach(var filter in cigarette.GetComponentsInChildren<MeshFilter>(true))
        {
            if(!filter.sharedMesh||filter.name=="Ember")continue;
            Bounds mesh=filter.sharedMesh.bounds;
            Matrix4x4 matrix=cigarette.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int i=0;i<8;i++)
            {
                Vector3 corner=mesh.center+Vector3.Scale(mesh.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                Vector3 point=matrix.MultiplyPoint3x4(corner);
                if(!found){result=new Bounds(point,Vector3.zero);found=true;}else result.Encapsulate(point);
            }
        }
        return result;
    }

    private static float Step => Mathf.Min(Time.unscaledDeltaTime, .1f);
    private static float Ease(float t) => Mathf.SmoothStep(0, 1, t);
    private IEnumerator Pause(float seconds)
    {
        for (float elapsed = 0; elapsed < seconds; elapsed += Step) yield return null;
    }
    private IEnumerator FirstDrag() { yield return Pause(.45f); yield return TakeDrag(); }
    private IEnumerator TakeDrag()
    {
        while (takingDrag) yield return null;
        takingDrag = true;
        for (float t = 0; t < .65f; t += Step) { drag = Ease(t / .65f); heat = Mathf.Lerp(.5f, 1, drag); yield return null; }
        drag = 1;
        heat = 1;
        Play(inhaleAudio);
        yield return Pause(.45f);
        for (float t = 0; t < .7f; t += Step) { drag = 1 - Ease(t / .7f); heat = Mathf.Lerp(.5f, 1, drag); yield return null; }
        drag = 0;
        heat = .5f;
        Play(exhaleAudio);
        takingDrag = false;
    }
    private static void Play(AudioSource source) { if (source && source.clip) source.PlayOneShot(source.clip); }
    private IEnumerator Say(int index, float seconds)
    {
        CurrentLine = index;
        var script=secondIntermission?secondLines:Lines;
        subtitle.text = script[index];
        lineNumber.text = $"{index + 1:00} / {script.Length:00}";
        subtitle.fontSize = secondIntermission ? (index == 5 ? 31 : 25) : (index == 2 ? 42 : 25);
        subtitleMotion.SetFear(secondIntermission&&index>=5?1:0);
        subtitleGroup.alpha = 1;
        subtitle.ForceMeshUpdate();
        if (!script[index].StartsWith("[")) GetComponent<DialogueVoice>()?.Begin(subtitle, true, true);
        advance = false;
        yield return null;
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            if (advance || (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false)) break;
            yield return null;
        }
        GetComponent<DialogueVoice>()?.End();
        subtitleGroup.alpha = 0;
        CurrentLine = -1;
        yield return Pause(.15f);
    }
    public void AdvanceLine() { if (CurrentLine >= 0) advance = true; }

    private IEnumerator TurnRight(Vector3 target, float seconds)
    {
        Play(headTurnAudio);
        Vector3 from = desiredRotation.eulerAngles;
        Vector3 to = Quaternion.LookRotation(target - desiredPosition, Vector3.up).eulerAngles;
        float yaw = Mathf.Repeat(to.y - from.y, 360);
        float pitch = Mathf.DeltaAngle(from.x, to.x);
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            float t = Ease(elapsed / seconds);
            desiredRotation = Quaternion.Euler(from.x + pitch * t, from.y + yaw * t, 0);
            playerCamera.fieldOfView = Mathf.Lerp(58, 53, t);
            yield return null;
        }
        desiredRotation = Quaternion.LookRotation(target - desiredPosition, Vector3.up);
    }
    private IEnumerator MoveCamera(Vector3 destination, Quaternion rotation, float seconds, float targetFov=58)
    {
        Vector3 from = desiredPosition;
        Quaternion facing = desiredRotation;
        if (Quaternion.Angle(facing, rotation) > 3f) Play(headTurnAudio);
        float fov = playerCamera.fieldOfView;
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            float t = Ease(elapsed / seconds);
            desiredPosition = Vector3.Lerp(from, destination, t);
            desiredRotation = Quaternion.Slerp(facing, rotation, t);
            playerCamera.fieldOfView = Mathf.Lerp(fov, targetFov, t);
            yield return null;
        }
        desiredPosition = destination;
        desiredRotation = rotation;
        playerCamera.fieldOfView = targetFov;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        float t = Time.unscaledTime;
        float still = HasFinished ? 0 : 1;
        Vector3 sway = new Vector3(Mathf.Sin(t * .7f) * .004f, Mathf.Sin(t * 1.1f) * .005f, 0) * still;
        sway += new Vector3(Mathf.Sin(t * 5.3f) * .009f, Mathf.Sin(t * 10.6f) * .018f, 0) * walking;
        if(secondIntermission)
        {
            sway += new Vector3(Mathf.Sin(t*24.3f)*.009f,Mathf.Sin(t*29.7f)*.012f,0)*panic
                +Vector3.up*Mathf.Sin(t*16.5f)*.028f*walking;
            if(collapseMusic&&collapseMusic.isPlaying)collapseMusic.volume=collapseMusicGain*(1-Mathf.Clamp01(FadeAmount));
        }
        float collapseShake=secondIntermission&&CurrentShot==Shot.Collapse?Mathf.Sin(buildingCollapse.Progress*Mathf.PI)*.8f:0;
        playerCamera.transform.SetPositionAndRotation(desiredPosition + desiredRotation * sway,
            desiredRotation * Quaternion.Euler(Mathf.Sin(t*22.7f)*(.24f*panic+collapseShake*.3f),Mathf.Sin(t*17.3f)*.18f*panic,Mathf.Sin(t * .8f) * .12f * still+Mathf.Sin(t*15.1f)*(.28f*panic+collapseShake)));
        if (inHand)
        {
            Vector3 pose = Vector3.Lerp(heldPosition, new Vector3(.07f, -.045f, .25f), drag);
            pose.y += Mathf.Sin(t * 1.3f) * .004f;
            pose += new Vector3(Mathf.Sin(t*18)*.012f,Mathf.Sin(t*24)*.009f,0)*panic;
            pose += new Vector3(0,.12f,.18f)*cigaretteThrow;
            // The second scene's head turns and zoom do not carry the hand around the world.
            Transform handFrame=secondIntermission?openingPose:playerCamera.transform;
            cigarette.position = handFrame.TransformPoint(pose);
            cigarette.rotation = handFrame.rotation * Quaternion.Euler(heldEuler + new Vector3(-32*cigaretteThrow, 0, drag * 8));
        }
        if (cigaretteSmoke) cigaretteSmoke.transform.rotation = Quaternion.LookRotation(Vector3.up);
        if (ember)
        {
            ember.GetPropertyBlock(emberProperties);
            Color glow = Color.Lerp(new Color(.07f, .016f, .008f), new Color(4, .48f, .035f), heat);
            emberProperties.SetColor("_BaseColor", glow);
            emberProperties.SetColor("_EmissionColor", glow * heat);
            ember.SetPropertyBlock(emberProperties);
        }
        if (emberLight) emberLight.intensity = heat * .16f;
    }

    private IEnumerator FadeTo(float target, float seconds)
    {
        float from = FadeAmount;
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            SetFade(Mathf.Lerp(from, target, Ease(elapsed / seconds)));
            yield return null;
        }
        SetFade(target);
    }
    private void SetFade(float amount)
    {
        FadeAmount = amount;
        if (fade) fade.color = new Color(0, 0, 0, amount);
        if (windAudio) windAudio.volume = windVolume * (1f - Mathf.Clamp01(amount));
    }

    private void BuildUI()
    {
        var root = new GameObject("Intermission cinematic UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        ui = root.GetComponent<Canvas>();
        ui.renderMode = RenderMode.ScreenSpaceOverlay;
        ui.sortingOrder = 1000;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = .5f;
        ImageRect("Top letterbox", root.transform, new Vector2(0, .955f), Vector2.one, Color.black);
        ImageRect("Bottom letterbox", root.transform, Vector2.zero, new Vector2(1, .045f), Color.black);
        var panel = ImageRect("Monologue panel", root.transform, new Vector2(.1f, 0), new Vector2(.9f, 0), OperatorDialogueStyle.Background);
        panel.rectTransform.pivot = new Vector2(.5f, 0);
        panel.rectTransform.anchoredPosition = new Vector2(0, 30);
        panel.rectTransform.sizeDelta = new Vector2(0, 170);
        var shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(0, -5);
        ImageRect("Top dialogue rule", panel.transform, new Vector2(0, 1), Vector2.one, OperatorDialogueStyle.TopRule).rectTransform.sizeDelta = new Vector2(0, 1);
        ImageRect("Bottom dialogue rule", panel.transform, Vector2.zero, Vector2.right, OperatorDialogueStyle.BottomRule).rectTransform.sizeDelta = new Vector2(0, 1);
        var speaker = DialogueLabel(panel.transform, "Speaker", "OPERATOR C", 13, new Vector2(26, -22), new Vector2(650, 22), new Vector2(0, 1));
        speaker.color = OperatorDialogueStyle.Speaker; speaker.characterSpacing = 3;
        lineNumber = DialogueLabel(panel.transform, "Line", "", 12, new Vector2(-26, -22), new Vector2(110, 22), Vector2.one);
        lineNumber.alignment = TextAlignmentOptions.Right; lineNumber.color = OperatorDialogueStyle.Secondary;
        subtitleGroup = panel.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0;
        subtitleGroup.blocksRaycasts = subtitleGroup.interactable = false;
        var rect = new GameObject("Monologue", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(panel.transform, false);rect.anchorMin = Vector2.zero;rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(26, 36);rect.offsetMax = new Vector2(-26, -44);
        subtitle = rect.gameObject.AddComponent<TextMeshProUGUI>();
        subtitle.font = subtitleFont ? subtitleFont : TMP_Settings.defaultFontAsset;
        subtitle.alignment = TextAlignmentOptions.MidlineLeft;
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        OperatorDialogueStyle.ApplyText(subtitle);
        subtitle.richText=true;
        if(secondIntermission)subtitle.overrideColorTags=false;
        subtitle.raycastTarget = false;
        subtitleMotion=subtitle.gameObject.AddComponent<RetroSubtitleText>();
        var hint = DialogueLabel(panel.transform, "Continue hint", "[ SPACE ]  NEXT LINE", 12, new Vector2(-26, 14), new Vector2(550, 20), Vector2.right);
        hint.alignment = TextAlignmentOptions.Right; hint.characterSpacing = 2; hint.color = OperatorDialogueStyle.Secondary;
        fade = ImageRect("Fade to black", root.transform, Vector2.zero, Vector2.one, Color.clear);
    }
    private TMP_Text DialogueLabel(Transform parent, string name, string content, float size, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = dimensions;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = subtitleFont ? subtitleFont : TMP_Settings.defaultFontAsset;
        text.text = content; text.fontSize = size; text.raycastTarget = false;
        return text;
    }
    private static Image ImageRect(string name, Transform parent, Vector2 min, Vector2 max, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);rect.anchorMin = min;rect.anchorMax = max;rect.offsetMin = rect.offsetMax = Vector2.zero;
        var image = rect.gameObject.AddComponent<Image>();image.color = color;image.raycastTarget = false;
        return image;
    }

    private void OnDisable()
    {
        GameplaySceneAudio.For(this)?.SetScriptedWalking(false);
        foreach (var source in new[] { inhaleAudio, exhaleAudio, headTurnAudio }) if (source) source.Stop();
        if (windAudio) { windAudio.Stop(); if (initialized) windAudio.volume = windVolume; }
        takingDrag = false;
        GetComponent<DialogueVoice>()?.End();
        if (!initialized) return;
        StopAllCoroutines();
        if(droppedCigaretteBody)
        {
            droppedCigaretteBody.isKinematic=true;droppedCigaretteBody.detectCollisions=false;
            Destroy(droppedCigaretteBody);
        }
        if(droppedCigaretteCollider){droppedCigaretteCollider.enabled=false;Destroy(droppedCigaretteCollider);}
        if(secondIntermission&&streetAudio)streetAudio.Stop();
        if(secondIntermission&&streetMaleAudio)streetMaleAudio.Stop();
        if(collapseMusic)collapseMusic.Stop();
        if(secondIntermission&&buildingCollapse)buildingCollapse.enabled=false;
        if (cigaretteSmoke) cigaretteSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (playerCamera) { playerCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation); playerCamera.fieldOfView = savedFov; }
        if (cigarette) { cigarette.SetPositionAndRotation(savedCigarettePosition, savedCigaretteRotation); cigarette.localScale = savedCigaretteScale; cigarette.gameObject.SetActive(savedCigaretteActive); }
        for (int i = 0; i < gameplayToSuspend.Length; i++) if (gameplayToSuspend[i]) gameplayToSuspend[i].enabled = savedBehaviours[i];
        if (playerHUD) playerHUD.enabled = savedHUD;
        Cursor.lockState = savedCursor;
        Cursor.visible = savedCursorVisible;
        if (ui) Destroy(ui.gameObject);
        initialized = false;
    }
}
