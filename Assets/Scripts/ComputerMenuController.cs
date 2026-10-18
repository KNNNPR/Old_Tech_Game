using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Room overview, camera flight, physical computer power and the monitor interface.</summary>
public class ComputerMenuController : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform overviewPose, computerPose, logo;
    [SerializeField] private float flightDuration = 1.5f;
    [SerializeField] private float overviewFov = 60f, computerFov = 42f;
    [SerializeField] private CanvasGroup monitorDisplay, menuPages, idleDisplay;
    [SerializeField] private Renderer screen;
    [SerializeField] private Light monitorLight;
    [SerializeField] private Transform lever;
    [SerializeField] private TMP_Text hint;
    [SerializeField] private MainMenuController menu;
    [SerializeField] private Material hoverOutlineMaterial;
    [SerializeField, HideInInspector] private MenuHoverOutline computerOutline, leverOutline;
    private MenuCameraSway cameraSway;
    private ComputerMenuTarget.TargetKind? hoveredTarget;
    private bool focused, powered = true, moving, draggingLever, leverToggled;
    private float flightTime, powerLevel = 1f;
    private Vector3 flightStart, dragStart;
    private Quaternion rotationStart, leverOn;
    private float fovStart;
    private MaterialPropertyBlock screenProperties;
    private readonly RaycastHit[] hits = new RaycastHit[32];
    private const int InteractionMask = 1 << 30;

    public bool IsFocused => focused && !moving;
    public bool IsPowered => powered;
    public bool IsMoving => moving;
    public bool CanUseMenu => IsFocused && powered;

    private void Awake()
    {
        screenProperties = new MaterialPropertyBlock();
        leverOn = lever.localRotation;
        EnsureAmbientMotion();
        cameraSway.SetBasePose(overviewPose.position, overviewPose.rotation);
        menuCamera.fieldOfView = overviewFov;
        if (logo.IsChildOf(menuCamera.transform)) logo.SetParent(null, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        menuPages.alpha = 0; menuPages.blocksRaycasts = menuPages.interactable = false;
        idleDisplay.alpha = 1; idleDisplay.blocksRaycasts = false;
        CreateHoverOutlines();
    }

    private void OnEnable()
    {
        // Restore hover rendering if Unity reloads these scripts during Play Mode.
        if (screenProperties == null) screenProperties = new MaterialPropertyBlock();
        if (Application.isPlaying && (computerOutline == null || leverOutline == null)) CreateHoverOutlines();
        if (Application.isPlaying && menuCamera != null) EnsureAmbientMotion();
    }

    private void Update()
    {
        hoveredTarget = null;
        float dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
        if (moving) AdvanceFlight(dt);
        else
        {
            Transform pose = focused ? computerPose : overviewPose;
            cameraSway.SetBasePose(pose.position, pose.rotation);
        }
        // Apply before pointer raycasts, so interaction uses the camera that is actually rendered.
        cameraSway.Apply(Time.unscaledTime, focused, dt);
        powerLevel = Mathf.MoveTowards(powerLevel, powered ? 1f : 0f, dt * 3f);
        screen.GetPropertyBlock(screenProperties);
        screenProperties.SetFloat("_Power", powerLevel);
        screen.SetPropertyBlock(screenProperties);
        if (monitorLight) monitorLight.intensity = powerLevel * .8f;
        monitorDisplay.alpha = powerLevel;
        float visibility = CanUseMenu ? 1f : 0f;
        menuPages.alpha = Mathf.MoveTowards(menuPages.alpha, visibility, dt * 5f);
        menuPages.blocksRaycasts = menuPages.interactable = CanUseMenu;
        idleDisplay.alpha = Mathf.MoveTowards(idleDisplay.alpha, !focused && powered ? 1f : 0f, dt * 5f);
        lever.localRotation = Quaternion.Slerp(lever.localRotation,
            leverOn * Quaternion.Euler(0,0,powered ? 0 : 38), 1f - Mathf.Exp(-dt * 12f));
        if (!logo.gameObject.activeSelf) logo.gameObject.SetActive(true);
        if (moving || menu.IsLoading || menu.IsRebinding) { hint.text = ""; return; }

        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (draggingLever)
        {
            hoveredTarget = ComputerMenuTarget.TargetKind.PowerLever;
            float distance = Vector2.Distance(mouse.position.ReadValue(), dragStart);
            if (!leverToggled && distance > 18f) { TogglePower(); leverToggled = true; }
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (!leverToggled && distance < 18f) TogglePower();
                draggingLever = false;
            }
            return;
        }
        hint.text = focused ? "ESC  /  RETURN" : "";
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return;
        Ray ray = menuCamera.ScreenPointToRay(mouse.position.ReadValue());
        int count = Physics.RaycastNonAlloc(ray,hits,100f,InteractionMask,QueryTriggerInteraction.Collide);
        int nearest = -1; float distanceToHit = float.PositiveInfinity;
        for(int i=0;i<count;i++) if(hits[i].distance < distanceToHit) { nearest=i;distanceToHit=hits[i].distance; }
        if(nearest<0) return;
        var hit=hits[nearest];
        var letter=hit.collider.GetComponentInParent<FloatingLogoLetter>();
        if(letter)
        {
            Vector3 away = letter.transform.position - hit.point;
            away += -menuCamera.transform.forward * .05f;
            letter.Push(away.normalized * Mathf.Min(dt*60,2),mouse.delta.ReadValue());
            return;
        }
        var target=hit.collider.GetComponentInParent<ComputerMenuTarget>();
        if(!target) return;
        hoveredTarget = target.kind;
        if(target.kind==ComputerMenuTarget.TargetKind.PowerLever)
        {
            hint.text=powered?"DRAG LEVER  /  POWER OFF":"DRAG LEVER  /  POWER ON";
            if(mouse.leftButton.wasPressedThisFrame)
            {
                draggingLever=true;leverToggled=false;dragStart=mouse.position.ReadValue();
            }
        }
        else if(!focused)
        {
            hint.text=powered?"CLICK COMPUTER":"COMPUTER OFF  /  USE RED LEVER";
            if(powered&&mouse.leftButton.wasPressedThisFrame) EnterComputer();
        }
    }

    private void CreateHoverOutlines()
    {
        if (hoverOutlineMaterial == null || (computerOutline != null && leverOutline != null)) return;
        Renderer body = null;
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name == "MysticComputer") { body = renderer; break; }
            if (body != null) break;
        }
        if (computerOutline == null)
        {
            computerOutline = gameObject.AddComponent<MenuHoverOutline>();
            computerOutline.Configure(hoverOutlineMaterial, body, screen);
        }
        if (leverOutline == null && lever != null)
        {
            leverOutline = gameObject.AddComponent<MenuHoverOutline>();
            leverOutline.Configure(hoverOutlineMaterial, lever.GetComponentInChildren<MeshRenderer>());
        }
    }

    private void EnsureAmbientMotion()
    {
        if (cameraSway == null)
        {
            cameraSway = menuCamera.GetComponent<MenuCameraSway>();
            if (cameraSway == null) cameraSway = menuCamera.gameObject.AddComponent<MenuCameraSway>();
        }
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var light in root.GetComponentsInChildren<Light>())
        {
            if (light.type != LightType.Directional || !light.enabled) continue;
            if (!light.TryGetComponent<MenuLightSway>(out _)) light.gameObject.AddComponent<MenuLightSway>();
            return;
        }
    }

    private void LateUpdate()
    {
        if (computerOutline != null) computerOutline.SetHovered(hoveredTarget == ComputerMenuTarget.TargetKind.Computer);
        if (leverOutline != null) leverOutline.SetHovered(hoveredTarget == ComputerMenuTarget.TargetKind.PowerLever);
    }

    private void OnDisable()
    {
        if (cameraSway != null) cameraSway.RestoreBasePose();
        if (computerOutline != null) computerOutline.SetHovered(false);
        if (leverOutline != null) leverOutline.SetHovered(false);
    }

    public void EnterComputer()
    {
        if(!powered || focused || menu.IsLoading) return;
        BeginFlight(true);
    }

    public void ReturnToOverview()
    {
        if(menu.IsLoading) return;
        menu.CloseSettings();
        EventSystem.current?.SetSelectedGameObject(null);
        if(!focused && !moving) return;
        BeginFlight(false);
    }

    public void TogglePower()
    {
        powered=!powered;
        if(!powered)
        {
            menu.CloseSettings();
            if(focused) ReturnToOverview();
        }
    }

    private void BeginFlight(bool towardsComputer)
    {
        focused=towardsComputer;moving=true;flightTime=0;
        flightStart=cameraSway.BasePosition;rotationStart=cameraSway.BaseRotation;fovStart=menuCamera.fieldOfView;
        menuPages.blocksRaycasts=menuPages.interactable=false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void AdvanceFlight(float dt)
    {
        flightTime+=dt;
        float linear=Mathf.Clamp01(flightTime/Mathf.Max(.1f,flightDuration));
        float t=linear*linear*(3f-2f*linear);
        Transform destination=focused?computerPose:overviewPose;
        Vector3 position=Vector3.Lerp(flightStart,destination.position,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.16f);
        cameraSway.SetBasePose(position,Quaternion.Slerp(rotationStart,destination.rotation,t));
        menuCamera.fieldOfView=Mathf.Lerp(fovStart,focused?computerFov:overviewFov,t);
        if(linear>=1f)
        {
            moving=false;
            cameraSway.SetBasePose(destination.position,destination.rotation);
        }
    }
}
