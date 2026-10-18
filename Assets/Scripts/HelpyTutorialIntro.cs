using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Scene-local introduction; only runs where this component is placed.</summary>
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

    [Header("Camera")]
    [SerializeField, Min(.1f)] private float turnDuration = .5f;
    [SerializeField, Min(.1f)] private float approachDuration = 1.5f;
    [SerializeField, Min(.1f)] private float returnDuration = 1.25f;
    [SerializeField, Range(25,65)] private float closeupFov = 42f;

    [Header("Dialogue / Space advances one line")]
    [SerializeField] private bool autoAdvance = true;
    [SerializeField, Min(1)] private float minimumLineDuration = 3f;
    [SerializeField, Min(.01f)] private float secondsPerCharacter = .055f;
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

    private Vector3 homePosition;
    private Quaternion homeRotation;
    private float homeFov;
    private bool controllerWasEnabled, controlWasEnabled, interactionWasEnabled, hudWasEnabled;
    private CursorLockMode cursorLock;
    private bool cursorVisible;
    private CanvasGroup subtitles;
    private TMP_Text dialogueText, lineNumber;
    private bool advanceRequested;

    private IEnumerator Start()
    {
        if (!playerCamera || !player || !interactor || !robot || lines == null || lines.Length == 0)
        {
            Debug.LogError("Helpy introduction is missing its TUTORIAL scene references.",this);
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
        IsPlaying = true;
        player.SetControlEnabled(false);
        // Disabling also suspends head bob and gravity while the camera is away from the player.
        player.enabled = false;
        interactor.InputEnabled = false;
        if (playerHUD) playerHUD.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BuildSubtitles();

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

        subtitles.alpha = 1;
        for (int i=0;i<lines.Length;i++)
        {
            CurrentLine = i;
            dialogueText.text = lines[i];
            lineNumber.text = $"{i+1:00} / {lines.Length:00}";
            advanceRequested = false;
            dialogueText.ForceMeshUpdate();
            float duration = Mathf.Max(minimumLineDuration,1.6f + dialogueText.textInfo.characterCount * secondsPerCharacter);
            float elapsed = 0;
            // One key press is consumed only by the current line, even during a very short frame.
            yield return null;
            while (!advanceRequested && !(Keyboard.current?.spaceKey.wasPressedThisFrame ?? false)
                   && (!autoAdvance || elapsed < duration))
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime,.1f);
                yield return null;
            }
        }
        CurrentLine = -1;
        for (float t=0;t<.2f;t+=Mathf.Min(Time.unscaledDeltaTime,.1f))
        {
            subtitles.alpha = 1f-t/.2f;
            yield return null;
        }
        subtitles.alpha = 0;
        Transform parent = playerCamera.transform.parent;
        Vector3 returnPosition = parent ? parent.TransformPoint(homePosition) : homePosition;
        Quaternion returnRotation = parent ? parent.rotation * homeRotation : homeRotation;
        yield return MoveCamera(returnPosition,returnRotation,homeFov,returnDuration,faceControl,approachControl);
        RestorePlayer();
        HasFinished = true;
    }

    /// <summary>Also usable by an on-screen accessibility button; never skips the whole introduction.</summary>
    public void AdvanceLine()
    {
        if (IsPlaying && CurrentLine >= 0) advanceRequested = true;
    }

    private MeshRenderer FindFace()
    {
        foreach (var mesh in robot.GetComponentsInChildren<MeshRenderer>(true))
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
        if (!IsPlaying) return;
        IsPlaying = false;
        CurrentLine = -1;
        if (playerCamera)
        {
            playerCamera.transform.localPosition = homePosition;
            playerCamera.transform.localRotation = homeRotation;
            playerCamera.fieldOfView = homeFov;
        }
        if (player) { player.SetControlEnabled(controlWasEnabled); player.enabled = controllerWasEnabled; }
        if (interactor) interactor.InputEnabled = interactionWasEnabled;
        if (playerHUD) playerHUD.enabled = hudWasEnabled;
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
        if (subtitles) Destroy(subtitles.gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestorePlayer();
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
        var hint = Text(panel,"Continue hint",12,new Vector2(-26,14),new Vector2(240,20),Vector2.right);
        hint.rectTransform.pivot = Vector2.right;hint.alignment = TextAlignmentOptions.Right;
        hint.text = "[ SPACE ]  NEXT LINE";hint.characterSpacing = 2;hint.color = new Color(.29f,.68f,.42f);
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
