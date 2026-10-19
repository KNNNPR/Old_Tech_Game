using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Two wrong verdicts interrupt the shift with the authored balcony-door threat.</summary>
[DefaultExecutionOrder(2000)]
public sealed class SortingFailureCinematic : MonoBehaviour
{
    public enum Phase { Waiting, Snap, Knocks, Opening, Blackout }
    [SerializeField] private SortingManager sorting;
    [SerializeField] private ShiftManager shift;
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Camera view;
    [SerializeField] private Transform balconyDoor, doorAim, sidePose;
    [SerializeField] private AnimationClip doorMotion;
    [SerializeField] private AudioSource knockAudio, openingAudio;
    [SerializeField] private float[] knockTimes;
    [SerializeField] private float openingStart = 1.0833334f;
    public Phase CurrentPhase { get; private set; }
    public bool IsPlaying => CurrentPhase != Phase.Waiting;
    public int KnocksPlayed { get; private set; }
    private Vector3 posePosition;
    private Quaternion poseRotation;
    private CanvasGroup black;

    private void OnEnable() { if (sorting) sorting.CardSorted += OnSorted; }
    private void OnSorted(CardData data, bool correct)
    {
        if (correct || IsPlaying || !sorting || sorting.MistakeCount < 2) return;
        if (!interactor || !controller || !view || !balconyDoor || !sidePose || !doorAim || !doorMotion)
        { Debug.LogError("Sorting failure cinematic is missing scene references.",this); return; }
        StartCoroutine(Play());
    }
    private IEnumerator Play()
    {
        CurrentPhase = Phase.Snap;
        shift?.AbortShift();
        // Release station ownership before acquiring the camera, including the recorder's separate input loop.
        foreach (var root in gameObject.scene.GetRootGameObjects())
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is RecorderInspectionView || behaviour is HelpyTutorialIntro || behaviour is HelpyReturnDialogue || behaviour is PlayerFaint)
            { behaviour.StopAllCoroutines(); behaviour.enabled = false; }
        interactor.ExitFocus(); interactor.StopAllCoroutines(); interactor.InputEnabled = false;
        interactor.SetExternalPrompt(null,""); interactor.enabled = false;
        controller.SetControlEnabled(false); controller.enabled = false;
        GameplaySceneAudio.For(this)?.SetScriptedWalking(false);
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        foreach (var door in balconyDoor.GetComponents<AnimatedDoor>()) door.enabled = false;
        foreach (var animator in balconyDoor.GetComponents<Animator>()) animator.enabled = false;
        posePosition = view.transform.position; poseRotation = view.transform.rotation;
        Quaternion look = Quaternion.LookRotation(doorAim.position - posePosition,Vector3.up);
        yield return Turn(look,.22f);
        doorMotion.SampleAnimation(balconyDoor.gameObject,0);
        CurrentPhase = Phase.Knocks;
        int nextKnock = 0;
        const float speed = .72f;
        for (float time = 0; time < openingStart; time += Time.unscaledDeltaTime * speed)
        {
            doorMotion.SampleAnimation(balconyDoor.gameObject,time);
            while (knockTimes != null && nextKnock < knockTimes.Length && time >= knockTimes[nextKnock])
            {
                if (knockAudio && knockAudio.clip) knockAudio.PlayOneShot(knockAudio.clip);
                nextKnock++; KnocksPlayed++;
            }
            yield return null;
        }
        while (knockTimes != null && nextKnock < knockTimes.Length)
        {
            if (knockAudio && knockAudio.clip) knockAudio.PlayOneShot(knockAudio.clip);
            nextKnock++; KnocksPlayed++;
        }
        doorMotion.SampleAnimation(balconyDoor.gameObject,openingStart);
        CurrentPhase = Phase.Opening;
        if (openingAudio && openingAudio.clip) openingAudio.Play();
        Vector3 from = posePosition; Quaternion rotation = poseRotation; float fromFov=view.fieldOfView;
        float lastKnock = knockTimes != null && knockTimes.Length > 0 ? knockTimes[knockTimes.Length-1] : 0;
        float knockTail = knockAudio && knockAudio.clip ? knockAudio.clip.length - (openingStart-lastKnock)/speed : 0;
        float openingSeconds = Mathf.Max(.85f,Mathf.Max(knockTail,openingAudio && openingAudio.clip ? openingAudio.clip.length : 0));
        for (float elapsed = 0; elapsed < openingSeconds; elapsed += Time.unscaledDeltaTime)
        {
            float move = Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.5f));
            posePosition = Vector3.Lerp(from,sidePose.position,move);
            poseRotation = Quaternion.Slerp(rotation,sidePose.rotation,move);
            view.fieldOfView = Mathf.Lerp(fromFov,65,move);
            doorMotion.SampleAnimation(balconyDoor.gameObject,Mathf.Lerp(openingStart,doorMotion.length,Mathf.Clamp01(elapsed/.85f)));
            yield return null;
        }
        doorMotion.SampleAnimation(balconyDoor.gameObject,doorMotion.length);
        CurrentPhase = Phase.Blackout;
        black = BlackOverlay(); black.alpha = 1;
        yield return null;
        const string defeat = "GAME_OVER";
        if (Application.CanStreamedLevelBeLoaded(defeat)) yield return SceneManager.LoadSceneAsync(defeat);
        else Debug.LogError("GAME_OVER must be enabled in Build Settings.",this);
    }
    private IEnumerator Turn(Quaternion target, float duration)
    {
        Quaternion from = poseRotation;
        for(float elapsed=0;elapsed<duration;elapsed+=Time.unscaledDeltaTime)
        { poseRotation=Quaternion.Slerp(from,target,Mathf.SmoothStep(0,1,elapsed/duration));yield return null; }
        poseRotation=target;
    }
    private void LateUpdate() { if(IsPlaying && view)view.transform.SetPositionAndRotation(posePosition,poseRotation); }
    private CanvasGroup BlackOverlay()
    {
        var root=new GameObject("Fatal blackout",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));
        root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        var image=new GameObject("Black",typeof(RectTransform),typeof(Image));image.transform.SetParent(root.transform,false);
        var rect=image.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
        image.GetComponent<Image>().color=Color.black;image.GetComponent<Image>().raycastTarget=false;
        return root.GetComponent<CanvasGroup>();
    }
    private void OnDisable()
    {
        if (sorting) sorting.CardSorted -= OnSorted;
        StopAllCoroutines(); if(knockAudio)knockAudio.Stop();if(openingAudio)openingAudio.Stop();
    }
}
