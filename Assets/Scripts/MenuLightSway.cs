using UnityEngine;

/// <summary>A slow pendulum motion moves the key light and its shadows around the authored pose.</summary>
[RequireComponent(typeof(Light))]
public class MenuLightSway : MonoBehaviour
{
    [SerializeField] private Vector2 swingAngles = new Vector2(6f, 4f);
    [SerializeField, Min(0f)] private float speed = .8f;
    [SerializeField, Min(0f)] private float pendulumLength = 1.2f;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private bool hasRestPose;

    private void OnEnable()
    {
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        hasRestPose = true;
    }

    private void Update() => Apply(Time.unscaledTime);

    public void Apply(float time)
    {
        if (!hasRestPose) return;
        float t = time * speed;
        Quaternion swing = Quaternion.Euler(Mathf.Sin(t) * swingAngles.x, 0f,
            Mathf.Sin(t * .81f + 1.2f) * swingAngles.y);
        Vector3 suspension = Vector3.down * pendulumLength;
        transform.localPosition = restPosition + swing * suspension - suspension;
        transform.localRotation = swing * restRotation;
    }

    private void OnDisable()
    {
        if (!hasRestPose) return;
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
        hasRestPose = false;
    }
}
