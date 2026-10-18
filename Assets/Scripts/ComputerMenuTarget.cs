using UnityEngine;

/// <summary>Dedicated menu hit targets avoid the room's gameplay colliders.</summary>
public class ComputerMenuTarget : MonoBehaviour
{
    public enum TargetKind { Computer, PowerLever }
    public TargetKind kind;
}
