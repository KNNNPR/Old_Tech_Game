using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>The second Helpy encounter, independently configured in HELPYDIALOGUE.</summary>
[DefaultExecutionOrder(-100)]
public sealed class HelpyReturnDialogue : MonoBehaviour
{
    public enum Speaker { Helpy, Player }
    public enum ConversationStage { Appearing, Approaching, Speaking, Departing, Returning, Finished }
    [Serializable]
    public sealed class Line
    {
        public Speaker speaker;
        [TextArea(2, 4)] public string text;
        public Line(Speaker speaker, string text) { this.speaker = speaker; this.text = text; }
    }

    [Header("Scene references")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private FirstPersonController player;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private Canvas playerHUD;
    [SerializeField] private Transform robot;
    [SerializeField] private Transform landingPose;
    [SerializeField] private Renderer face;
    [SerializeField, HideInInspector] private Vector3 faceNormal = Vector3.down;
    [SerializeField] private AnimationClip appearance;
    [SerializeField] private AnimationClip idle;
    [SerializeField] private TMP_FontAsset subtitleFont;
    [Header("Camera")]
    [SerializeField, Min(.1f)] private float turnDuration = .55f;
    [SerializeField, Min(.1f)] private float approachDuration = 1.5f;
    [SerializeField, Min(.1f)] private float returnDuration = 1.25f;
    [SerializeField, Range(25, 65)] private float closeupFov = 42f;
    [Header("Readable typewriter / Space advances one line")]
    [SerializeField, Min(.01f)] private float secondsPerCharacter = .075f;
    [SerializeField, Min(1f)] private float minimumLineDuration = 4f;
    [SerializeField] private bool autoAdvance = true;
    [SerializeField]
    private Line[] lines =
    {
        new Line(Speaker.Helpy, "Hello again, Operator! How's it going?"),
        new Line(Speaker.Player, "Helpy, what these cards are even <b><i><color=#B5FFA0>about</color></i></b>? It kinds of creeps me out"),
        new Line(Speaker.Helpy, "No worries! They can't harm you, despite the <i><color=#B5FFA0>corrupted</color></i> down there"),
        new Line(Speaker.Player, "D-Down WHAT?"),
        new Line(Speaker.Helpy, "Well the people that consumed the <i><color=#B5FFA0>corruption</color></i> info got...  <b><i><color=#B5FFA0>Mesmerized</color></i></b>"),
        new Line(Speaker.Helpy, "That's why you are here <i><color=#B5FFA0>sorting</color></i> them right here"),
        new Line(Speaker.Player, ". . ."),
        new Line(Speaker.Helpy, "You didn't read the <b><i><color=#B5FFA0>fine print</color></i></b>, did you?"),
        new Line(Speaker.Helpy, "Jokes on you then! Well, bye Operator C")
    };

    public bool IsPlaying { get; private set; }
    public bool HasFinished { get; private set; }
    public int CurrentLine { get; private set; } = -1;
    public Speaker CurrentSpeaker => CurrentLine >= 0 ? lines[CurrentLine].speaker : Speaker.Helpy;
    public ConversationStage Stage { get; private set; }
    private Vector3 homePosition, landingPosition, clipEndPosition;
    private Quaternion homeRotation, landingRotation;
    private float homeFov, idleTime;
    private bool locked, playerWasEnabled, controlWasEnabled, interactionWasEnabled, hudWasEnabled;
    private bool characterWasEnabled, robotWasActive, cursorWasVisible, advanceRequested, idlePlaying;
    private CursorLockMode cursorLock;
    private CharacterController character;
    private Animator[] animators;
    private bool[] animatorEnabled;
    private CanvasGroup subtitles;
    private TMP_Text dialogueText, speakerText, lineNumber;
    private TMP_Text continueHint;
    private Image dialogueBackground, topRule, bottomRule;
    private Vector3? approachControl, faceControl;

    private IEnumerator Start()
    {
        if (!playerCamera || !player || !interactor || !robot || !landingPose || !face || !appearance || !idle || lines == null || lines.Length == 0)
        {
            Debug.LogError("HELPYDIALOGUE conversation is missing scene references or animation clips.", this);
            yield break;
        }
        homePosition = playerCamera.transform.localPosition;
        homeRotation = playerCamera.transform.localRotation;
        homeFov = playerCamera.fieldOfView;
        playerWasEnabled = player.enabled; controlWasEnabled = player.ControlEnabled;
        interactionWasEnabled = interactor.InputEnabled; hudWasEnabled = playerHUD && playerHUD.enabled;
        character = player.GetComponent<CharacterController>(); characterWasEnabled = character && character.enabled;
        cursorLock = Cursor.lockState; cursorWasVisible = Cursor.visible;
        landingPosition = robot.parent ? robot.parent.InverseTransformPoint(landingPose.position) : landingPose.position;
        landingRotation = robot.parent ? Quaternion.Inverse(robot.parent.rotation) * landingPose.rotation : landingPose.rotation;
        robotWasActive = robot.gameObject.activeSelf;
        locked = IsPlaying = true;
        player.SetControlEnabled(false); player.enabled = false;
        if (character) character.enabled = false;
        interactor.InputEnabled = false;
        if (playerHUD) playerHUD.enabled = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        BuildSubtitles();

        animators = robot.GetComponentsInChildren<Animator>(true);
        animatorEnabled = new bool[animators.Length];
        for (int i = 0; i < animators.Length; i++) { animatorEnabled[i] = animators[i].enabled; animators[i].enabled = false; }
        robot.gameObject.SetActive(true);
        appearance.SampleAnimation(robot.gameObject, appearance.length);
        clipEndPosition = robot.localPosition;
        robot.SetLocalPositionAndRotation(landingPosition, landingRotation);
        Vector3 landingFace = face.bounds.center;
        SampleAppearance(0);

        Stage = ConversationStage.Appearing;
        yield return MoveCamera(playerCamera.transform.position, Quaternion.LookRotation(landingFace - playerCamera.transform.position), homeFov, turnDuration);
        yield return PlayAppearance(false);
        idlePlaying = true; idleTime = 0;
        Stage = ConversationStage.Approaching;
        Vector3 centre = face.bounds.center;
        Vector3 normal = face.transform.TransformDirection(faceNormal).normalized;
        float height = Mathf.Max(face.bounds.size.y, .3f);
        float distance = height / (2 * Mathf.Tan(closeupFov * Mathf.Deg2Rad * .5f) * .58f);
        Vector3 closePosition = centre + normal * distance + Vector3.up * height * .04f;
        Quaternion closeRotation = Quaternion.LookRotation(centre - Vector3.up * height * .12f - closePosition, Vector3.up);
        if (Vector3.Dot(playerCamera.transform.position - centre, normal) < height * .2f)
        {
            approachControl = playerCamera.transform.position + Vector3.up * height * 1.2f;
            faceControl = closePosition + Vector3.up * height * .55f;
        }
        yield return MoveCamera(closePosition, closeRotation, closeupFov, approachDuration, approachControl, faceControl);
        Stage = ConversationStage.Speaking;
        for (int i = 0; i < lines.Length; i++) yield return Say(i);
        CurrentLine = -1; subtitles.alpha = 0;
        Stage = ConversationStage.Departing;
        idlePlaying = false;
        yield return PlayAppearance(true);
        robot.gameObject.SetActive(false);
        Stage = ConversationStage.Returning;
        Transform parent = playerCamera.transform.parent;
        yield return MoveCamera(parent ? parent.TransformPoint(homePosition) : homePosition,
            parent ? parent.rotation * homeRotation : homeRotation, homeFov, returnDuration, faceControl, approachControl);
        RestorePlayer();
        Stage = ConversationStage.Finished; IsPlaying = false; HasFinished = true;
        if (subtitles) Destroy(subtitles.gameObject);
    }

    private void SampleAppearance(float time)
    {
        appearance.SampleAnimation(robot.gameObject, time);
        // The tutorial clip uses absolute root coordinates; preserve this scene's landing location.
        robot.SetLocalPositionAndRotation(landingPosition + robot.localPosition - clipEndPosition, landingRotation);
    }
    private IEnumerator PlayAppearance(bool reverse)
    {
        for (float elapsed = 0; elapsed < appearance.length; elapsed += Time.unscaledDeltaTime)
        {
            SampleAppearance(reverse ? appearance.length - elapsed : elapsed);
            yield return null;
        }
        SampleAppearance(reverse ? 0 : appearance.length);
    }
    private void LateUpdate()
    {
        if (!idlePlaying || !robot) return;
        idleTime += Time.unscaledDeltaTime;
        idle.SampleAnimation(robot.gameObject, Mathf.Repeat(idleTime, Mathf.Max(.01f, idle.length)));
        robot.SetLocalPositionAndRotation(landingPosition, landingRotation);
    }
    private IEnumerator Say(int index)
    {
        CurrentLine = index; subtitles.alpha = 1; advanceRequested = false;
        ApplySpeakerStyle(lines[index].speaker);
        lineNumber.text = $"{index + 1:00} / {lines.Length:00}";
        dialogueText.text = lines[index].text; dialogueText.maxVisibleCharacters = 0; dialogueText.ForceMeshUpdate();
        bool isOperator = lines[index].speaker == Speaker.Player;
        GetComponent<DialogueVoice>()?.Begin(dialogueText, isOperator, false, isOperator ? playerCamera.transform : face.transform);
        int count = dialogueText.textInfo.characterCount;
        float elapsed = 0, duration = count * secondsPerCharacter + Mathf.Max(minimumLineDuration, count * .045f);
        yield return null;
        while (!advanceRequested && !(Keyboard.current?.spaceKey.wasPressedThisFrame ?? false) && (!autoAdvance || elapsed < duration))
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f);
            dialogueText.maxVisibleCharacters = Mathf.Min(count, (int)(elapsed / secondsPerCharacter));
            yield return null;
        }
        GetComponent<DialogueVoice>()?.End();
        CurrentLine = -1;
    }
    public void AdvanceLine()
    {
        if (IsPlaying && CurrentLine >= 0) advanceRequested = true;
    }
    private void ApplySpeakerStyle(Speaker speaker)
    {
        bool isPlayer = speaker == Speaker.Player;
        speakerText.text = isPlayer ? "OPERATOR C" : "HELPY  /  FIELD ASSISTANT";
        speakerText.color = isPlayer ? OperatorDialogueStyle.Speaker : new Color(.49f, .95f, .63f);
        dialogueBackground.color = isPlayer ? OperatorDialogueStyle.Background : new Color(.002f, .016f, .009f, .96f);
        topRule.color = isPlayer ? OperatorDialogueStyle.TopRule : new Color(.24f, .85f, .4f, .5f);
        bottomRule.color = isPlayer ? OperatorDialogueStyle.BottomRule : new Color(.1f, .5f, .24f, .3f);
        lineNumber.color = isPlayer ? OperatorDialogueStyle.Secondary : new Color(.25f, .65f, .4f);
        continueHint.color = isPlayer ? OperatorDialogueStyle.Secondary : new Color(.29f, .68f, .42f);
        if (isPlayer) OperatorDialogueStyle.ApplyText(dialogueText);
        else
        {
            dialogueText.color = Color.white;
            dialogueText.overrideColorTags = false;
            dialogueText.enableVertexGradient = true;
            dialogueText.colorGradient = new VertexGradient(new Color(.7f, 1f, .72f), new Color(.6f, 1f, .68f), new Color(.16f, .74f, .34f), new Color(.22f, .85f, .4f));
        }
    }
    private IEnumerator MoveCamera(Vector3 destination, Quaternion rotation, float fov, float seconds, Vector3? first = null, Vector3? second = null)
    {
        Vector3 from = playerCamera.transform.position; Quaternion fromRotation = playerCamera.transform.rotation;
        float fromFov = playerCamera.fieldOfView;
        for (float elapsed = 0; elapsed < seconds; elapsed += Mathf.Min(Time.unscaledDeltaTime, .1f))
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / seconds), u = 1 - t;
            Vector3 position = first.HasValue && second.HasValue ? u * u * u * from + 3 * u * u * t * first.Value + 3 * u * t * t * second.Value + t * t * t * destination : Vector3.Lerp(from, destination, t);
            playerCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(fromRotation, rotation, t));
            playerCamera.fieldOfView = Mathf.Lerp(fromFov, fov, t);
            yield return null;
        }
        playerCamera.transform.SetPositionAndRotation(destination, rotation); playerCamera.fieldOfView = fov;
    }
    private void RestorePlayer()
    {
        if (!locked) return;
        locked = false;
        if (playerCamera) { playerCamera.transform.SetLocalPositionAndRotation(homePosition, homeRotation); playerCamera.fieldOfView = homeFov; }
        if (player) { player.SetControlEnabled(controlWasEnabled); player.enabled = playerWasEnabled; }
        if (character) character.enabled = characterWasEnabled;
        if (interactor) interactor.InputEnabled = interactionWasEnabled;
        if (playerHUD) playerHUD.enabled = hudWasEnabled;
        Cursor.lockState = cursorLock; Cursor.visible = cursorWasVisible;
    }
    private void OnDisable()
    {
        GetComponent<DialogueVoice>()?.End();
        StopAllCoroutines(); idlePlaying = false;
        RestorePlayer();
        if (IsPlaying && robot) { robot.SetLocalPositionAndRotation(landingPosition, landingRotation); robot.gameObject.SetActive(robotWasActive); }
        if (animators != null) for (int i = 0; i < animators.Length; i++) if (animators[i]) animators[i].enabled = animatorEnabled[i];
        IsPlaying = false; CurrentLine = -1;
        if (subtitles) Destroy(subtitles.gameObject);
    }

    private void BuildSubtitles()
    {
        var root = new GameObject("Helpy return subtitles", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
        var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .45f;
        subtitles = root.GetComponent<CanvasGroup>(); subtitles.alpha = 0; subtitles.blocksRaycasts = subtitles.interactable = false;
        var panel = Rect("Dialogue panel", root.transform, new Vector2(.1f, 0), new Vector2(.9f, 0), new Vector2(0, 30), new Vector2(0, 170));
        panel.pivot = new Vector2(.5f, 0);
        dialogueBackground = panel.gameObject.AddComponent<Image>(); dialogueBackground.color = new Color(.002f, .016f, .009f, .96f); dialogueBackground.raycastTarget = false;
        var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .65f); shadow.effectDistance = new Vector2(0, -5);
        topRule = Rule(panel, 1, new Color(.24f, .85f, .4f, .5f)); bottomRule = Rule(panel, 0, new Color(.1f, .5f, .24f, .3f));
        speakerText = Text(panel, "Speaker", 13, new Vector2(26, -22), new Vector2(650, 22), new Vector2(0, 1)); speakerText.characterSpacing = 3;
        lineNumber = Text(panel, "Line", 12, new Vector2(-26, -22), new Vector2(110, 22), Vector2.one);
        lineNumber.gameObject.SetActive(false); // счётчик реплик убран
        lineNumber.alignment = TextAlignmentOptions.Right; lineNumber.color = new Color(.25f, .65f, .4f);
        dialogueText = Text(panel, "Dialogue", 27, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f));
        var body = dialogueText.rectTransform; body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(26, 36); body.offsetMax = new Vector2(-26, -44);
        dialogueText.alignment = TextAlignmentOptions.MidlineLeft; dialogueText.enableVertexGradient = true;
        dialogueText.colorGradient = new VertexGradient(new Color(.7f, 1f, .72f), new Color(.6f, 1f, .68f), new Color(.16f, .74f, .34f), new Color(.22f, .85f, .4f));
        dialogueText.gameObject.AddComponent<RetroSubtitleText>();
        var hint = Text(panel, "Continue hint", 12, new Vector2(-26, 14), new Vector2(550, 20), Vector2.right);
        hint.alignment = TextAlignmentOptions.Right; hint.text = "[ SPACE ]  NEXT LINE"; hint.characterSpacing = 2; hint.color = new Color(.29f, .68f, .42f);
        continueHint = hint;
    }
    private TMP_Text Text(Transform parent, string name, float size, Vector2 position, Vector2 dimensions, Vector2 anchor)
    {
        var rect = Rect(name, parent, anchor, anchor, position, dimensions); rect.pivot = anchor;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = subtitleFont ? subtitleFont : TMP_Settings.defaultFontAsset;
        text.fontSize = size; text.color = Color.white; text.raycastTarget = false; text.richText = true; text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static Image Rule(RectTransform panel, float y, Color color)
    {
        var rule = Rect("Phosphor rule", panel, new Vector2(0, y), new Vector2(1, y), Vector2.zero, new Vector2(0, 1));
        var image = rule.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        image.enabled = false; // зелёные полоски убраны
        return image;
    }
}