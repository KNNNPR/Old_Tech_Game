using UnityEngine;

/// <summary>Plays the existing house_fall clip once at the story cue, with low resolution dust.</summary>
[DisallowMultipleComponent]
public sealed class IntermissionBuildingCollapse : MonoBehaviour
{
    [SerializeField] private Transform[] buildingParts;
    [SerializeField] private Transform animatedBuilding;
    [SerializeField] private AnimationClip fallAnimation;
    [SerializeField] private Animator buildingAnimator;
    [SerializeField] private Bounds buildingBounds;
    [SerializeField] private ParticleSystem dustCloud, debris;
    [SerializeField] private AudioSource collapseAudio;
    private Vector3 savedPosition,savedScale,localFocus,cloudCentre;
    private Quaternion savedRotation;
    private bool animatorWasEnabled;
    private float elapsed, nextDust;
    private bool initialized, falling;
    private System.Random random;
    public bool HasLanded { get; private set; }
    public float Progress { get; private set; }
    public Vector3 TrackingPoint
    {
        get
        {
            Vector3 point=animatedBuilding?animatedBuilding.TransformPoint(localFocus):buildingBounds.center;
            point.y=Mathf.Max(point.y,buildingBounds.min.y+buildingBounds.size.y*.1f);
            return point;
        }
    }

    private void Awake()
    {
        if(!animatedBuilding||!fallAnimation||buildingParts==null||buildingParts.Length==0){enabled=false;return;}
        savedPosition=animatedBuilding.localPosition;savedRotation=animatedBuilding.localRotation;savedScale=animatedBuilding.localScale;
        if(buildingAnimator){animatorWasEnabled=buildingAnimator.enabled;buildingAnimator.enabled=false;}
        localFocus=animatedBuilding.InverseTransformPoint(buildingBounds.center+Vector3.up*buildingBounds.size.y*.22f);
        cloudCentre=buildingBounds.center;
        random=new System.Random(9461); initialized=true;
    }
    public void BeginCollapse()
    {
        if(!initialized||falling||HasLanded)return;
        elapsed=nextDust=0; falling=true;
        if(collapseAudio&&collapseAudio.clip)collapseAudio.Play();
        if(dustCloud)dustCloud.Play();
        if(debris)debris.Play();
        fallAnimation.SampleAnimation(animatedBuilding.gameObject,0);
    }
    private void Update()
    {
        if(!falling)return;
        elapsed+=Mathf.Min(Time.unscaledDeltaTime,.1f);
        Progress=Mathf.Clamp01(elapsed/Mathf.Max(.01f,fallAnimation.length));
        fallAnimation.SampleAnimation(animatedBuilding.gameObject,Mathf.Min(elapsed,fallAnimation.length));
        var current=buildingParts[0].GetComponent<Renderer>().bounds;
        for(int i=1;i<buildingParts.Length;i++)current.Encapsulate(buildingParts[i].GetComponent<Renderer>().bounds);
        cloudCentre=current.center;
        if(elapsed>=nextDust)
        {
            nextDust=elapsed+.14f;
            EmitDust(5+Mathf.RoundToInt(Progress*9),false);
        }
        if(Progress>=1)
        {
            falling=false;HasLanded=true;EmitDust(100,true);
            if(debris)
            {
                for(int i=0;i<65;i++)
                {
                    var point=BasePoint();
                    debris.Emit(new ParticleSystem.EmitParams{position=point,velocity=new Vector3(Range(-5,5),Range(4,12),Range(-5,5)),applyShapeToPosition=false},1);
                }
            }
        }
    }
    private Vector3 BasePoint()=>new Vector3(cloudCentre.x+Range(-.4f,.4f)*buildingBounds.size.x,buildingBounds.min.y+1,cloudCentre.z+Range(-.4f,.4f)*buildingBounds.size.z);
    private void EmitDust(int count,bool impact)
    {
        if(!dustCloud)return;
        for(int i=0;i<count;i++)
        {
            Vector3 position=BasePoint();
            if(!impact)position.y+=Range(0,.25f)*buildingBounds.size.y*(1-Progress);
            dustCloud.Emit(new ParticleSystem.EmitParams{position=position,velocity=new Vector3(Range(-2,2),Range(2,impact?8:5),Range(-2,2)),startSize=Range(3,impact?9:6),applyShapeToPosition=false},1);
        }
    }
    private float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    private void OnDisable()
    {
        falling=false;
        if(collapseAudio)collapseAudio.Stop();
        if(dustCloud)dustCloud.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(debris)debris.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(!initialized)return;
        if(animatedBuilding){animatedBuilding.SetLocalPositionAndRotation(savedPosition,savedRotation);animatedBuilding.localScale=savedScale;}
        if(buildingAnimator)buildingAnimator.enabled=animatorWasEnabled;
        initialized=false;
    }
}
