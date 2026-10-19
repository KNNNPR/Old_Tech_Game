using UnityEngine;

/// <summary>Gently bends the free edge of a wall poster while its upper strip stays pinned.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
public sealed class PinnedPosterWind : MonoBehaviour
{
    [SerializeField] private Mesh deformableMesh;
    [SerializeField] private IntermissionWind wind;
    [SerializeField] private Vector3 localOutward = Vector3.up;
    [SerializeField, Range(.05f, .4f)] private float pinnedTopFraction = .16f;
    [SerializeField, Range(.001f, .06f)] private float amplitude = .025f;
    private MeshFilter filter;
    private Mesh originalMesh, animatedMesh;
    private Vector3[] rest, vertices;
    private float[] weights, horizontal;
    private Vector3 localOffsetPerMeter;
    private float nextUpdate;

    private void OnEnable()
    {
        if (!deformableMesh) return;
        filter = GetComponent<MeshFilter>();
        originalMesh = filter.sharedMesh;
        animatedMesh = Instantiate(deformableMesh);
        animatedMesh.name = "Pinned poster wind (runtime)";
        animatedMesh.MarkDynamic();
        rest = deformableMesh.vertices;
        vertices = new Vector3[rest.Length];
        weights = new float[rest.Length]; horizontal = new float[rest.Length];
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        Vector3 right = Vector3.Cross(Vector3.up, transform.TransformDirection(localOutward)).normalized;
        for (int i = 0; i < rest.Length; i++)
        {
            Vector3 world = transform.TransformPoint(rest[i]);
            float x = Vector3.Dot(world, right);
            minY = Mathf.Min(minY, world.y); maxY = Mathf.Max(maxY, world.y);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
        }
        for (int i = 0; i < rest.Length; i++)
        {
            Vector3 world = transform.TransformPoint(rest[i]);
            float belowPin = (1 - Mathf.InverseLerp(minY, maxY, world.y) - pinnedTopFraction) / (1 - pinnedTopFraction);
            float free = Mathf.SmoothStep(0, 1, Mathf.Clamp01(belowPin));
            weights[i] = free * free;
            horizontal[i] = Mathf.InverseLerp(minX, maxX, Vector3.Dot(world, right));
        }
        localOffsetPerMeter = transform.InverseTransformVector(transform.TransformDirection(localOutward).normalized);
        filter.sharedMesh = animatedMesh;
        nextUpdate = 0;
        Apply(Time.unscaledTime);
    }

    private void LateUpdate()
    {
        if (!animatedMesh || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + 1f / 12;
        Apply(Time.unscaledTime);
    }

    private void Apply(float time)
    {
        float strength = wind ? Mathf.Lerp(.4f, 1, Mathf.InverseLerp(.85f, 2.8f, wind.WindSpeed)) : .55f;
        for (int i = 0; i < rest.Length; i++)
        {
            float ripple = .7f * (.5f + .5f * Mathf.Sin(time * 2.2f + weights[i] * 4.5f + horizontal[i] * 2.2f))
                         + .3f * (.5f + .5f * Mathf.Sin(time * 5.7f + weights[i] * 7.1f - horizontal[i] * 4.3f));
            // Offset only away from the wall; pinned vertices never move.
            vertices[i] = rest[i] + localOffsetPerMeter * (amplitude * strength * weights[i] * ripple);
        }
        animatedMesh.vertices = vertices;
        animatedMesh.RecalculateNormals();
        animatedMesh.RecalculateBounds();
    }

    private void OnDisable()
    {
        if (filter && animatedMesh && filter.sharedMesh == animatedMesh) filter.sharedMesh = originalMesh;
        if (animatedMesh) Destroy(animatedMesh);
        animatedMesh = null;
    }
}
