using UnityEngine;

/// <summary>Uses the authored door clip in both directions through the player's interaction ray.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Animator))]
public sealed class AnimatedDoor : MonoBehaviour, IInteractable
{
    [SerializeField] private AnimationClip motion;
    [SerializeField] private bool startsOpen;
    [SerializeField] private string timeParameter = "DoorProgress";
    [SerializeField] private string stateName = "Base Layer.door";
    private Animator animator;
    private int parameterHash;
    private float progress;
    private bool targetOpen;

    public bool IsOpen => progress >= .999f;
    public bool IsMoving => Mathf.Abs(progress-(targetOpen?1f:0f)) > .0001f;
    public float OpenAmount => progress;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!motion || !animator.runtimeAnimatorController)
        {
            Debug.LogError("AnimatedDoor needs door.anim and its configured Animator Controller.",this);
            enabled=false;
            return;
        }
        animator.applyRootMotion=false;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        parameterHash=Animator.StringToHash(timeParameter);
        targetOpen=startsOpen;progress=startsOpen?1f:0f;
        animator.Rebind();
        animator.SetFloat(parameterHash,progress);
        animator.Play(Animator.StringToHash(stateName),0,0);
        animator.Update(0);
    }

    private void Update()
    {
        progress=Mathf.MoveTowards(progress,targetOpen?1f:0f,Time.deltaTime/Mathf.Max(motion.length,.01f));
        animator.SetFloat(parameterHash,progress);
    }

    public string GetPrompt(PlayerInteractor interactor) => targetOpen?"[E] Close door":"[E] Open door";
    public bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled && animator && animator.enabled;
    public void Interact(PlayerInteractor interactor)
    {
        if (CanInteract(interactor)) targetOpen=!targetOpen;
    }
}
