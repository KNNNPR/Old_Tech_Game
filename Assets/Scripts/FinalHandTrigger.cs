using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>Day four: the hand is inserted freely; pulling the lever commits to reading and the final pull into the monitor.</summary>
[DefaultExecutionOrder(1000)]
public class FinalHandTrigger : MonoBehaviour
{
    public enum Phase { Idle, Focusing, Reading, Glancing, Pulling, Blackout }
    [SerializeField] private ItemSlot computerSlot;
    [SerializeField] private FeedLever lever;
    [SerializeField] private Transform screenTarget, readingPose, punchingAim;
    [SerializeField] private MonitorDisplay monitor;
    [SerializeField] private FourthShiftAwakening awakening;
    [SerializeField] private AudioSource punchSplash;
    [SerializeField] private ParticleSystem punchBlood;
    [TextArea, SerializeField] private string readingText = "HAND DETECTED.\nREADING...";
    [SerializeField] private float shakeSeconds = 3.2f;
    [SerializeField] private float shakeDegrees = 3.5f;
    [SerializeField] private float flySeconds = 1.25f;
    [SerializeField] private float stopDistance = .02f;
    [SerializeField, Range(0,1)] private float fadeFrom = .78f;
    [SerializeField] private float fovKick = 20;
    public UnityEvent onHandInserted;

    public Phase CurrentPhase { get; private set; }
    public bool HasTriggered => CurrentPhase != Phase.Idle;
    public int PunchSplashesPlayed { get; private set; }
    public int BloodBurstsPlayed { get; private set; }
    private bool cameraLocked, feedFinished;
    private Camera view;
    private Vector3 posePosition;
    private Quaternion poseRotation;
    private float shake;
    private CanvasGroup black;
    private Pickupable readingHand;
    private PlayerInteractor interactor;

    private void OnEnable()
    {
        if (!lever) return;
        lever.FeedStarted += OnFeedStarted;
        lever.PunchLanded += OnPunch;
        lever.CardFed += OnCardFed;
    }
    private void OnFeedStarted()
    {
        if (HasTriggered || GameFlow.CurrentShift != 3 || !computerSlot || !computerSlot.Current
            || !computerSlot.Current.GetComponent<FinalHand>() || awakening && !awakening.HasAwoken) return;
        if (!readingPose || !screenTarget || !punchingAim)
        { Debug.LogError("Final hand reading is missing its camera markers.",this); return; }
        readingHand=computerSlot.Current;lever.FeedPaused=true;
        CurrentPhase=Phase.Focusing;
        StartCoroutine(ReadHand());
    }
    private void OnPunch(Pickupable item)
    {
        if (!HasTriggered || item != readingHand) return;
        PunchSplashesPlayed++;
        if (punchSplash && punchSplash.clip) punchSplash.PlayOneShot(punchSplash.clip);
        if (punchBlood)
        {
            punchBlood.transform.position=readingHand.transform.TransformPoint(FinalHand.ModelCenter(readingHand.transform))+Vector3.up*.03f;
            punchBlood.Emit(16);BloodBurstsPlayed++;
        }
    }
    private void OnCardFed(Pickupable item)
    {
        if (!HasTriggered || item != readingHand) return;
        feedFinished=true;
        if(monitor)monitor.ShowSystemText(readingText);
    }
    private IEnumerator ReadHand()
    {
        interactor=FindFirstObjectByType<PlayerInteractor>();
        view=interactor?interactor.PlayerCamera:null;
        if(!view){lever.FeedPaused=false;Debug.LogError("Final hand reading needs the player camera.",this);yield break;}
        var controller=interactor.Controller;
        foreach(var root in gameObject.scene.GetRootGameObjects())
        foreach(var recorder in root.GetComponentsInChildren<RecorderInspectionView>(true))recorder.enabled=false;
        interactor.ExitFocus();interactor.StopAllCoroutines();interactor.InputEnabled=false;
        interactor.SetExternalPrompt(null,"");interactor.enabled=false;
        if(controller){controller.SetControlEnabled(false);controller.enabled=false;}
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
        computerSlot.Locked=true;
        posePosition=view.transform.position;poseRotation=view.transform.rotation;cameraLocked=true;
        black=CreateBlackOverlay();
        onHandInserted?.Invoke();
        if(monitor)monitor.ShowSystemText(readingText);

        GameplaySceneAudio.For(this)?.PlayCameraMove();

        Vector3 from=posePosition;Quaternion rotation=poseRotation;float fromFov=view.fieldOfView;
        for(float elapsed=0;elapsed<.8f;elapsed+=Time.unscaledDeltaTime)
        {
            float t=Mathf.SmoothStep(0,1,elapsed/.8f);
            posePosition=Vector3.Lerp(from,readingPose.position,t);
            poseRotation=Quaternion.Slerp(rotation,readingPose.rotation,t);
            view.fieldOfView=Mathf.Lerp(fromFov,50,t);
            yield return null;
        }
        posePosition=readingPose.position;poseRotation=readingPose.rotation;
        CurrentPhase=Phase.Reading;lever.FeedPaused=false;
        while(!feedFinished || lever.IsBusy)yield return null;

        CurrentPhase=Phase.Glancing;
        for(int glance=0;glance<4;glance++)
        {
            Vector3 target=(glance%2==0?screenTarget: punchingAim).position;
            Quaternion look=Quaternion.LookRotation(target-posePosition,Vector3.up);
            rotation=poseRotation;
            float duration=Mathf.Max(.2f,shakeSeconds/4);
            for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/(duration*.7f)));
                poseRotation=Quaternion.Slerp(rotation,look,t);
                shake=Mathf.Lerp(.45f,shakeDegrees,(glance+elapsed/duration)/4);
                yield return null;
            }
        }

        CurrentPhase=Phase.Pulling;
        from=posePosition;rotation=poseRotation;fromFov=view.fieldOfView;
        Vector3 into=(screenTarget.position-from).normalized;
        Vector3 end=screenTarget.position-into*stopDistance;
        Vector3 bend=Vector3.Lerp(from,end,.45f)+Vector3.up*.7f;
        Quaternion endRotation=Quaternion.LookRotation(into,Vector3.up);
        for(float elapsed=0;elapsed<flySeconds;elapsed+=Time.unscaledDeltaTime)
        {
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.05f,flySeconds)),speed=t*t*t;
            posePosition=(1-speed)*(1-speed)*from+2*(1-speed)*speed*bend+speed*speed*end;
            poseRotation=Quaternion.Slerp(rotation,endRotation,Mathf.SmoothStep(0,1,Mathf.Clamp01(t*2)));
            view.fieldOfView=fromFov+fovKick*speed;
            shake=shakeDegrees*(1+t*.7f);
            black.alpha=Mathf.InverseLerp(fadeFrom,1,t);
            yield return null;
        }
        posePosition=end;CurrentPhase=Phase.Blackout;black.alpha=1;
        yield return new WaitForSecondsRealtime(.3f);
        GameFlow.Advance();
    }
    private void LateUpdate()
    {
        if(!cameraLocked||!view)return;
        float t=Time.unscaledTime*26;
        var angles=new Vector3(Mathf.Sin(t*1.1f),Mathf.Sin(t*1.37f),Mathf.Sin(t*.83f))*shake;
        var offset=new Vector3(Mathf.Sin(t*.7f),Mathf.Sin(t*.9f),0)*(shake*.004f);
        view.transform.SetPositionAndRotation(posePosition+offset,poseRotation*Quaternion.Euler(angles));
    }
    private CanvasGroup CreateBlackOverlay()
    {
        var root=new GameObject("Final hand blackout",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));root.transform.SetParent(transform,false);
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
        var cover=new GameObject("Black",typeof(RectTransform),typeof(Image));cover.transform.SetParent(root.transform,false);
        var rect=cover.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        cover.GetComponent<Image>().color=Color.black;cover.GetComponent<Image>().raycastTarget=false;
        var group=root.GetComponent<CanvasGroup>();group.alpha=0;group.blocksRaycasts=false;return group;
    }
    private void OnDisable()
    {
        if(lever){lever.FeedStarted-=OnFeedStarted;lever.PunchLanded-=OnPunch;lever.CardFed-=OnCardFed;lever.FeedPaused=false;}
        StopAllCoroutines();if(punchSplash)punchSplash.Stop();if(black)Destroy(black.gameObject);
        if(punchBlood)punchBlood.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
