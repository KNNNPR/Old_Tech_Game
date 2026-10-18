using UnityEngine;

/// <summary>Кассета с записью. Потом её можно будет вставить в магнитофон.</summary>
[RequireComponent(typeof(Pickupable))]
public class Cassette : MonoBehaviour
{
    [SerializeField] private AudioClip clip;

    public AudioClip Clip
    {
        get => clip;
        set => clip = value;
    }
}
