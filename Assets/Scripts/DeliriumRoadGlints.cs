using UnityEngine;

/// <summary>Small, stationary surface glints. Only their brightness changes; nothing falls or floats off the road.</summary>
public sealed class DeliriumRoadGlints:MonoBehaviour
{
    [SerializeField] private ParticleSystem stars,beads;
    [SerializeField] private Vector3[] starPositions,beadPositions;
    private ParticleSystem.Particle[] starParticles,beadParticles;
    private float started;

    private void OnEnable()
    {
        started=Time.time;
        starParticles=new ParticleSystem.Particle[starPositions?.Length??0];
        beadParticles=new ParticleSystem.Particle[beadPositions?.Length??0];
        if(stars)stars.Play();if(beads)beads.Play();
    }
    private void LateUpdate()
    {
        float t=Time.time-started;
        Draw(stars,starPositions,starParticles,t,false);
        Draw(beads,beadPositions,beadParticles,t,true);
    }
    private static void Draw(ParticleSystem system,Vector3[] points,ParticleSystem.Particle[] particles,float time,bool round)
    {
        if(!system||points==null||particles==null)return;
        for(int i=0;i<particles.Length;i++)
        {
            float seed=Mathf.Repeat(i*.618034f+(round?.31f:.07f),1);
            float phase=i*2.399963f;
            float wave=Mathf.Max(0,Mathf.Sin(time*Mathf.Lerp(round?1.2f:.65f,round?2.6f:2.05f,seed)+phase));
            float brightness=Mathf.Pow(wave,round?8:12);
            particles[i].position=points[i];
            particles[i].startLifetime=particles[i].remainingLifetime=1000;
            particles[i].startSize=Mathf.Lerp(round?.022f:.05f,round?.047f:.095f,seed)*Mathf.Lerp(.45f,1,brightness);
            particles[i].startColor=new Color(1,1,1,brightness);
            particles[i].rotation=(i%3)*45;
            particles[i].randomSeed=(uint)(i+1);
        }
        system.SetParticles(particles,particles.Length);
    }
    private void OnDisable()
    {
        if(stars)stars.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        if(beads)beads.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
