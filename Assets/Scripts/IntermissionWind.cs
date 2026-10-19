using UnityEngine;

/// <summary>Outdoor gusts for the first Intermission: coarse debris and wind-blown cigarette smoke.</summary>
[DefaultExecutionOrder(100)]
public sealed class IntermissionWind : MonoBehaviour
{
    [SerializeField] private IntermissionCinematic cinematic;
    [SerializeField] private Transform overlook;
    [SerializeField] private ParticleSystem dust, paper, cigaretteSmoke;
    [SerializeField] private Vector3 direction = new Vector3(.18f, 0, -1);
    [SerializeField, Min(0)] private float calmSpeed = .85f;
    [SerializeField, Min(0)] private float gustSpeed = 2.8f;

    public float WindSpeed { get; private set; }
    private float nextUpdate;
    private bool stopped;
    private ParticleSystem.MinMaxCurve smokeX, smokeY, smokeZ;
    private ParticleSystemSimulationSpace smokeSpace;
    private bool smokeVelocityEnabled, initialized;

    private void OnEnable()
    {
        direction = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
        if (cigaretteSmoke)
        {
            var velocity = cigaretteSmoke.velocityOverLifetime;
            smokeX = velocity.x; smokeY = velocity.y; smokeZ = velocity.z;
            smokeSpace = velocity.space; smokeVelocityEnabled = velocity.enabled;
        }
        nextUpdate = 0; stopped = false; initialized = true;
        ApplyGust();
        if (dust) { dust.Simulate(4, true, true); dust.Play(); }
        if (paper)
        {
            paper.Simulate(4, true, true); paper.Play();
            if (overlook)
            {
                // Start two scraps in the city view so a sparse wind is readable from the opening shot.
                Vector3 forward = Vector3.ProjectOnPlane(overlook.forward, Vector3.up).normalized;
                paper.Emit(new ParticleSystem.EmitParams { position = overlook.position + forward * 4.5f - direction * .8f - Vector3.up * .25f, applyShapeToPosition = false }, 1);
                paper.Emit(new ParticleSystem.EmitParams { position = overlook.position + forward * 7 - direction * 1.6f + Vector3.up * .3f, applyShapeToPosition = false }, 1);
            }
        }
    }

    private void LateUpdate()
    {
        if (cinematic && cinematic.HasFinished)
        {
            if (!stopped) { StopDebris(); stopped = true; }
            return;
        }
        // Change the gust in small steps, matching the coarse particle silhouettes.
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 1f / 12;
        ApplyGust();
    }

    private void ApplyGust()
    {
        float t = Time.unscaledTime;
        float gust = Mathf.Pow(.5f + .5f * Mathf.Sin(t * .83f - .8f), 3);
        WindSpeed = Mathf.Lerp(calmSpeed, gustSpeed, gust);
        float flutter = Mathf.Sin(t * 2.3f) * .055f;
        SetVelocity(dust, direction * WindSpeed, -.045f + flutter, .76f, 1.2f);
        SetVelocity(paper, direction * WindSpeed * .72f, -.12f + flutter * 2, .7f, 1.15f);
        if (dust) { var emission = dust.emission; emission.rateOverTime = Mathf.Lerp(5, 11, gust); }
        if (paper) { var emission = paper.emission; emission.rateOverTime = Mathf.Lerp(.3f, .65f, gust); }
        // These are world velocities: the smoke bends in the same direction after the head turns.
        SetVelocity(cigaretteSmoke, direction * WindSpeed * .075f, .065f, .8f, 1.15f);
    }

    private static void SetVelocity(ParticleSystem particles, Vector3 wind, float lift, float min, float max)
    {
        if (!particles) return;
        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = Range(wind.x * min, wind.x * max);
        velocity.z = Range(wind.z * min, wind.z * max);
        velocity.y = Range(lift - .025f, lift + .025f);
    }
    private static ParticleSystem.MinMaxCurve Range(float a, float b) => new ParticleSystem.MinMaxCurve(Mathf.Min(a,b), Mathf.Max(a,b));

    private void StopDebris()
    {
        if (dust) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (paper) paper.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        WindSpeed = 0;
    }

    private void OnDisable()
    {
        StopDebris();
        if (initialized && cigaretteSmoke)
        {
            var velocity = cigaretteSmoke.velocityOverLifetime;
            velocity.x = smokeX; velocity.y = smokeY; velocity.z = smokeZ;
            velocity.space = smokeSpace; velocity.enabled = smokeVelocityEnabled;
        }
        initialized = false;
    }
}
