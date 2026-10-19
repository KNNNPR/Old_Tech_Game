using UnityEngine;

/// <summary>Each giant card drifts around its authored pose; the three road cards stay fixed.</summary>
public sealed class DeliriumCardDrift : MonoBehaviour
{
    [SerializeField] private bool gentle;
    [SerializeField] private float phase,speed=1;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float started;
    private void OnEnable(){restPosition=transform.localPosition;restRotation=transform.localRotation;started=Time.time;}
    private void Update()
    {
        float t=(Time.time-started)*speed;
        float amount=gentle?.055f:.22f;
        transform.localPosition=restPosition+Vector3.up*(Mathf.Sin(t*.65f+phase)-Mathf.Sin(phase))*amount;
        Vector3 angle=gentle?new Vector3(Mathf.Sin(t*.45f+phase)-Mathf.Sin(phase),Mathf.Sin(t*.3f)*.8f,Mathf.Sin(t*.5f)*.8f)
            :new Vector3(t*7,t*13,Mathf.Sin(t*.4f+phase)*8-Mathf.Sin(phase)*8);
        transform.localRotation=restRotation*Quaternion.Euler(angle);
    }
    private void OnDisable(){transform.localPosition=restPosition;transform.localRotation=restRotation;}
}
