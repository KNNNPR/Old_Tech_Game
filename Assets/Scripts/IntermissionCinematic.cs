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
    public enum Shot { Opening, TurningRight, Elevator, FinalDrag, Ashtray, Walking, Fading, Finished }
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
    [Header("Pacing")]
    [SerializeField, Min(.1f)] private float rightTurnSeconds = 1.6f;
    [SerializeField, Min(.1f)] private float walkSpeed = .85f;
    [SerializeField, Min(.1f)] private float fadeSeconds = 1.5f;
    [SerializeField] private TMP_FontAsset subtitleFont;
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

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
    private TMP_Text subtitle;
    private Image fade;
    private MaterialPropertyBlock emberProperties;

    private void Awake()
    {
        if (!playerCamera || !openingPose || !elevatorFocus || !ashtrayFocus || !cigaretteRest || !cigarette || !doorStop || !doorFocus)
        {
            Debug.LogError("Intermission cinematic needs its scene camera, props and shot markers.", this);
            enabled = false;
            return;
        }
        savedCameraPosition = playerCamera.transform.position;
        savedCameraRotation = playerCamera.transform.rotation;
        savedFov = playerCamera.fieldOfView;
        savedCigarettePosition = cigarette.position;
        savedCigaretteRotation = cigarette.rotation;
        savedCigaretteScale = cigarette.localScale;
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
        BuildUI();
        SetFade(1);
        initialized = true;
    }

    private IEnumerator Start()
    {
        if (!initialized) yield break;
        if (cigaretteSmoke) cigaretteSmoke.Play();
        CurrentShot = Shot.Opening;
        yield return FadeTo(0, .8f);
        StartCoroutine(FirstDrag());
        yield return Say(0, 2.8f);
        yield return Say(1, 3.8f);

        CurrentShot = Shot.TurningRight;
        yield return TurnRight(elevatorFocus.position, rightTurnSeconds);
        CurrentShot = Shot.Elevator;
        yield return Say(2, 1.1f);
        yield return Say(3, 2.8f);
        yield return Say(4, 3.4f);
        yield return Say(5, 4.3f);

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
        yield return Pause(.25f);
        CurrentShot = Shot.Fading;
        yield return FadeTo(1, fadeSeconds);
        CurrentShot = Shot.Finished;
        HasFinished = true;
        onFinished.Invoke();
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
        for (float t = 0; t < .65f; t += Step) { drag = Ease(t / .65f); heat = Mathf.Lerp(.5f, 1, drag); yield return null; }
        drag = 1;
        heat = 1;
        yield return Pause(.45f);
        for (float t = 0; t < .7f; t += Step) { drag = 1 - Ease(t / .7f); heat = Mathf.Lerp(.5f, 1, drag); yield return null; }
        drag = 0;
        heat = .5f;
    }
    private IEnumerator Say(int index, float seconds)
    {
        CurrentLine = index;
        subtitle.text = Lines[index];
        subtitle.fontSize = index == 2 ? 42 : 25;
        subtitleGroup.alpha = 1;
        subtitle.ForceMeshUpdate();
        advance = false;
        yield return null;
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            if (advance || (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false)) break;
            yield return null;
        }
        subtitleGroup.alpha = 0;
        CurrentLine = -1;
        yield return Pause(.15f);
    }
    public void AdvanceLine() { if (CurrentLine >= 0) advance = true; }

    private IEnumerator TurnRight(Vector3 target, float seconds)
    {
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
    private IEnumerator MoveCamera(Vector3 destination, Quaternion rotation, float seconds)
    {
        Vector3 from = desiredPosition;
        Quaternion facing = desiredRotation;
        float fov = playerCamera.fieldOfView;
        for (float elapsed = 0; elapsed < seconds; elapsed += Step)
        {
            float t = Ease(elapsed / seconds);
            desiredPosition = Vector3.Lerp(from, destination, t);
            desiredRotation = Quaternion.Slerp(facing, rotation, t);
            playerCamera.fieldOfView = Mathf.Lerp(fov, 58, t);
            yield return null;
        }
        desiredPosition = destination;
        desiredRotation = rotation;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        float t = Time.unscaledTime;
        float still = HasFinished ? 0 : 1;
        Vector3 sway = new Vector3(Mathf.Sin(t * .7f) * .004f, Mathf.Sin(t * 1.1f) * .005f, 0) * still;
        sway += new Vector3(Mathf.Sin(t * 5.3f) * .009f, Mathf.Sin(t * 10.6f) * .018f, 0) * walking;
        playerCamera.transform.SetPositionAndRotation(desiredPosition + desiredRotation * sway,
            desiredRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * .8f) * .12f * still));
        if (inHand)
        {
            Vector3 pose = Vector3.Lerp(heldPosition, new Vector3(.07f, -.045f, .25f), drag);
            pose.y += Mathf.Sin(t * 1.3f) * .004f;
            cigarette.position = playerCamera.transform.TransformPoint(pose);
            cigarette.rotation = playerCamera.transform.rotation * Quaternion.Euler(heldEuler + new Vector3(0, 0, drag * 8));
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
    private void SetFade(float amount) { FadeAmount = amount; if (fade) fade.color = new Color(0, 0, 0, amount); }

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
        var panel = ImageRect("Monologue panel", root.transform, new Vector2(.12f, .07f), new Vector2(.88f, .2f), new Color(.003f, .012f, .008f, .87f));
        subtitleGroup = panel.gameObject.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0;
        subtitleGroup.blocksRaycasts = subtitleGroup.interactable = false;
        var rect = new GameObject("Monologue", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(panel.transform, false);rect.anchorMin = Vector2.zero;rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(24, 15);rect.offsetMax = new Vector2(-24, -10);
        subtitle = rect.gameObject.AddComponent<TextMeshProUGUI>();
        subtitle.font = subtitleFont ? subtitleFont : TMP_Settings.defaultFontAsset;
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.textWrappingMode = TextWrappingModes.Normal;
        subtitle.color = new Color(.77f, .94f, .81f);
        subtitle.raycastTarget = false;
        subtitle.gameObject.AddComponent<RetroSubtitleText>();
        fade = ImageRect("Fade to black", root.transform, Vector2.zero, Vector2.one, Color.clear);
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
        if (!initialized) return;
        StopAllCoroutines();
        if (cigaretteSmoke) cigaretteSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (playerCamera) { playerCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation); playerCamera.fieldOfView = savedFov; }
        if (cigarette) { cigarette.SetPositionAndRotation(savedCigarettePosition, savedCigaretteRotation); cigarette.localScale = savedCigaretteScale; }
        for (int i = 0; i < gameplayToSuspend.Length; i++) if (gameplayToSuspend[i]) gameplayToSuspend[i].enabled = savedBehaviours[i];
        if (playerHUD) playerHUD.enabled = savedHUD;
        Cursor.lockState = savedCursor;
        Cursor.visible = savedCursorVisible;
        if (ui) Destroy(ui.gameObject);
        initialized = false;
    }
}
