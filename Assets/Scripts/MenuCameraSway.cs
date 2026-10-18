using UnityEngine;

/// <summary>Adds a small, non-accumulating sway to the pose supplied by the menu camera flight.</summary>
public class MenuCameraSway : MonoBehaviour
{
    [SerializeField] private Vector3 positionAmplitude = new Vector3(.012f, .01f, .004f);
    [SerializeField] private Vector3 angleAmplitude = new Vector3(.22f, .3f, .18f);
    [SerializeField, Min(0f)] private float speed = .65f;
    [SerializeField, Range(0f, 1f)] private float focusedMultiplier = .25f;
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private bool hasBasePose;
    private float swayWeight = 1f;

    public Vector3 BasePosition => hasBasePose ? basePosition : transform.position;
    public Quaternion BaseRotation => hasBasePose ? baseRotation : transform.rotation;

    public void SetBasePose(Vector3 position, Quaternion rotation)
    {
        basePosition = position;
        baseRotation = rotation;
        hasBasePose = true;
        RestoreBasePose();
    }

    public void Apply(float time, bool focused, float deltaTime)
    {
        if (!isActiveAndEnabled) return;
        if (!hasBasePose) SetBasePose(transform.position, transform.rotation);
        float blend = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) * 3.5f);
        swayWeight = Mathf.Lerp(swayWeight, focused ? focusedMultiplier : 1f, blend);
        float t = time * speed;
        Vector3 shift = new Vector3(Mathf.Sin(t * .73f) * positionAmplitude.x,
            Mathf.Sin(t) * positionAmplitude.y, Mathf.Sin(t * .51f) * positionAmplitude.z) * swayWeight;
        Vector3 angles = new Vector3(Mathf.Sin(t * .83f) * angleAmplitude.x,
            Mathf.Sin(t * .61f + 1.3f) * angleAmplitude.y,
            Mathf.Sin(t * .67f + .4f) * angleAmplitude.z) * swayWeight;
        transform.SetPositionAndRotation(basePosition + baseRotation * shift,
            baseRotation * Quaternion.Euler(angles));
    }

    public void RestoreBasePose()
    {
        if (hasBasePose) transform.SetPositionAndRotation(basePosition, baseRotation);
    }

    private void OnDisable() => RestoreBasePose();
}
