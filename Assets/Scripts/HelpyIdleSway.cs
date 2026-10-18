using UnityEngine;

/// <summary>Gentle idle motion around Helpy's screen, keeping his face in the dialogue framing.</summary>
public sealed class HelpyIdleSway : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 swingAngles = new Vector3(.55f,.35f,.8f);
    [SerializeField, Min(0)] private float speed = .85f;
    [SerializeField, Min(0)] private float bobHeight = .012f;
    private Vector3 restPosition, scaledPivot, pivotPosition;
    private Quaternion restRotation;
    private float startedAt;
    private bool hasRestPose;

    private void OnEnable()
    {
        if (!target) return;
        restPosition = target.localPosition;
        restRotation = target.localRotation;
        Vector3 centre = target.position;
        foreach (var mesh in target.GetComponentsInChildren<MeshRenderer>(true))
            if (mesh.name == "Cube.001") { centre = mesh.bounds.center; break; }
        scaledPivot = Vector3.Scale(target.InverseTransformPoint(centre),target.localScale);
        pivotPosition = restPosition + restRotation * scaledPivot;
        startedAt = Time.time;
        hasRestPose = true;
    }

    private void LateUpdate()
    {
        if (!hasRestPose || !target) return;
        float t = (Time.time-startedAt) * speed;
        Quaternion swing = Quaternion.Euler(Mathf.Sin(t*.83f)*swingAngles.x,
            Mathf.Sin(t*.61f)*swingAngles.y,Mathf.Sin(t)*swingAngles.z);
        Quaternion rotation = swing * restRotation;
        Vector3 bob = Vector3.up * (Mathf.Sin(t*1.1f)*bobHeight);
        if (target.parent) bob = target.parent.InverseTransformVector(bob);
        // Always derive the pose from the saved position, so motion cannot accumulate.
        target.SetLocalPositionAndRotation(pivotPosition-rotation*scaledPivot+bob,rotation);
    }

    private void OnDisable()
    {
        if (hasRestPose && target) target.SetLocalPositionAndRotation(restPosition,restRotation);
        hasRestPose = false;
    }
}
