using System.Collections.Generic;
using UnityEngine;

/// <summary>A damped spring keeps each FBX letter near its own floating home position.</summary>
public class FloatingLogoLetter : MonoBehaviour
{
    [SerializeField] private float phase;
    [SerializeField] private float maxOffset = .65f;
    [SerializeField] private float pushStrength = .14f;
    [SerializeField] private float hoverAmplitude = .045f;
    [SerializeField, ColorUsage(false, true)] private Color glowColor = new Color(.45f, .45f, .45f, 1f);
    [SerializeField, Range(0f, 1f)] private float collisionBounce = .45f;
    private Vector3 home;
    private Quaternion homeRotation;
    private Vector3 offset, velocity, tilt;
    private Renderer letterRenderer;
    private MaterialPropertyBlock properties;
    private Material originalMaterial, hoverMaterial;
    private float touchGlow;
    private BoxCollider collisionShape;
    private static readonly List<FloatingLogoLetter> activeLetters = new List<FloatingLogoLetter>();
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetLetters() => activeLetters.Clear();

    private void OnEnable()
    {
        collisionShape = GetComponentInChildren<BoxCollider>();
        if (!activeLetters.Contains(this)) activeLetters.Add(this);
    }

    private void Awake()
    {
        home = transform.localPosition;
        homeRotation = transform.localRotation;
        phase = transform.GetSiblingIndex() * 0.73f;
        letterRenderer = GetComponentInChildren<Renderer>();
        properties = new MaterialPropertyBlock();
        if (letterRenderer != null)
        {
            originalMaterial = letterRenderer.sharedMaterial;
            hoverMaterial = new Material(originalMaterial);
            hoverMaterial.EnableKeyword("_EMISSION");
            hoverMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            hoverMaterial.SetColor(EmissionColor, Color.black);
            letterRenderer.sharedMaterial = hoverMaterial;
            SetGlow(Color.black);
        }
    }

    public void Push(Vector3 worldDirection, Vector2 pointerDelta)
    {
        touchGlow = 1f;
        Vector3 local = transform.parent.InverseTransformDirection(worldDirection);
        velocity += Vector3.ClampMagnitude(local, 1f) * pushStrength;
        velocity += new Vector3(-pointerDelta.x, pointerDelta.y, 0) * (pushStrength * .0085f);
        velocity = Vector3.ClampMagnitude(velocity, 2.8f);
        tilt += new Vector3(pointerDelta.y, -pointerDelta.x, local.x * 28f) * 0.035f;
        tilt = Vector3.ClampMagnitude(tilt, 18f);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
        velocity += (-offset * 20f - velocity * 5f) * dt;
        offset = Vector3.ClampMagnitude(offset + velocity * dt, maxOffset);
        tilt = Vector3.Lerp(tilt, Vector3.zero, dt * 3f);
        float t = Time.unscaledTime;
        Vector3 hover = new Vector3(Mathf.Sin(t * 0.53f + phase) * .4f,
            Mathf.Sin(t * 0.85f + phase), Mathf.Cos(t * 0.6f + phase) * .55f) * hoverAmplitude;
        transform.localPosition = home + offset + hover;
        transform.localRotation = homeRotation * Quaternion.Euler(tilt + new Vector3(
            Mathf.Sin(t * 0.7f + phase), Mathf.Cos(t * 0.6f + phase) * 1.4f, Mathf.Sin(t * 0.5f + phase) * 1.1f));
        touchGlow = Mathf.MoveTowards(touchGlow, 0f, dt * 4f);
        SetGlow(glowColor * touchGlow);
    }

    private void LateUpdate()
    {
        // All spring and hover motion has finished before checking the final letter poses.
        if (activeLetters.Count > 0 && activeLetters[0] == this) ResolveCollisions();
    }

    private static void ResolveCollisions()
    {
        // Several passes let an impact propagate through a row without leaving overlaps.
        for (int iteration = 0; iteration < 6; iteration++)
        {
            bool touching = false;
            for (int i = 0; i < activeLetters.Count; i++)
            for (int j = i + 1; j < activeLetters.Count; j++)
            {
                var a = activeLetters[i];
                var b = activeLetters[j];
                if (a == null || b == null || a.transform.parent != b.transform.parent) continue;
                var shapeA = a.collisionShape;
                var shapeB = b.collisionShape;
                if (shapeA == null || shapeB == null || !shapeA.enabled || !shapeB.enabled) continue;
                if (!Physics.ComputePenetration(shapeA, shapeA.transform.position, shapeA.transform.rotation,
                    shapeB, shapeB.transform.position, shapeB.transform.rotation, out Vector3 normal, out float depth)) continue;

                touching = true;
                Vector3 separation = normal * ((depth + .0005f) * .5f);
                a.Displace(separation);
                b.Displace(-separation);

                // Equal mass response: transfer momentum only while the letters approach.
                Vector3 velocityA = a.ToWorldVector(a.velocity);
                Vector3 velocityB = b.ToWorldVector(b.velocity);
                float approach = Vector3.Dot(velocityA - velocityB, normal);
                if (approach >= 0f) continue;
                float bounce = Mathf.Min(a.collisionBounce, b.collisionBounce);
                Vector3 impulse = normal * (-approach * (1f + bounce) * .5f);
                a.velocity = a.ToLocalVector(velocityA + impulse);
                b.velocity = b.ToLocalVector(velocityB - impulse);
            }
            if (!touching) break;
        }
    }

    private Vector3 ToWorldVector(Vector3 value) => transform.parent ? transform.parent.TransformVector(value) : value;
    private Vector3 ToLocalVector(Vector3 value) => transform.parent ? transform.parent.InverseTransformVector(value) : value;

    private void Displace(Vector3 worldDelta)
    {
        // Keep the correction in the spring state so Update cannot undo the collision.
        offset += ToLocalVector(worldDelta);
        transform.position += worldDelta;
    }

    private void SetGlow(Color color)
    {
        if (letterRenderer == null || properties == null) return;
        letterRenderer.GetPropertyBlock(properties);
        properties.SetColor(EmissionColor, color);
        letterRenderer.SetPropertyBlock(properties);
    }

    private void OnDisable()
    {
        activeLetters.Remove(this);
        touchGlow = 0;
        SetGlow(Color.black);
    }

    private void OnDestroy()
    {
        if (hoverMaterial == null) return;
        if (letterRenderer != null) letterRenderer.sharedMaterial = originalMaterial;
        if (Application.isPlaying) Destroy(hoverMaterial);
        else DestroyImmediate(hoverMaterial);
    }
}
