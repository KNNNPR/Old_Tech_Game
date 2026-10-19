using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Feeds a cassette through the computer's side tube and holds it for pickup.</summary>
[DisallowMultipleComponent]
public sealed class CassetteTubeSpawner : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject cassettePrefab;
    [SerializeField] private Transform feedStart;
    [SerializeField] private Transform outputPose;
    [SerializeField] private Collider pickupTarget;
    [SerializeField, Min(.1f)] private float feedSeconds = 1.5f;
    [Header("Automatic supply; gameplay can call TrySpawnCassette instead")]
    [SerializeField] private bool automaticSpawning = true;
    [SerializeField, Min(0)] private float initialDelay = 2f;
    [SerializeField, Min(.1f)] private float spawnInterval = 8f;
    [SerializeField, Min(1)] private int maxLooseCassettes = 3;
    [SerializeField] private AudioSource spawnAudio;

    private readonly List<Cassette> issued = new List<Cassette>();
    private Cassette feeding;
    private Rigidbody feedingBody;
    private Collider[] feedingColliders;
    private bool[] colliderStates;
    private float timer, progress;
    private bool configured;

    public bool InTransit => feeding != null;
    public Cassette OutputCassette { get; private set; }
    public Cassette LastSpawnedCassette { get; private set; }
    public event Action<Cassette> CassetteReady;

    private void Awake()
    {
        configured = cassettePrefab && cassettePrefab.GetComponent<Cassette>()
            && cassettePrefab.GetComponent<Pickupable>() && cassettePrefab.GetComponent<Rigidbody>()
            && feedStart && outputPose;
        timer = initialDelay;
        SetPickupTarget(false);
        if (!configured) Debug.LogError("CassetteTubeSpawner needs the cassette prefab and both tube poses.", this);
    }

    private void Update()
    {
        if (!configured) return;
        if (feeding)
        {
            progress = Mathf.Min(1f, progress + Time.deltaTime / feedSeconds);
            MoveCassette(progress);
            if (progress >= 1f) CompleteFeed();
            return;
        }
        if (OutputCassette && (!OutputCassette.gameObject.activeInHierarchy || OutputCassette.GetComponent<Pickupable>().IsAttached))
        {
            OutputCassette = null;
            SetPickupTarget(false);
            timer = spawnInterval;
        }
        if (!automaticSpawning || OutputCassette) return;
        timer -= Time.deltaTime;
        if (timer <= 0f && !TrySpawnCassette()) timer = .5f;
    }

    /// <summary>Returns false while feeding, while the outlet is occupied, or at the loose-item limit.</summary>
    public bool TrySpawnCassette(AudioClip clip = null)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !configured || InTransit) return false;
        if (OutputCassette && OutputCassette.GetComponent<Pickupable>().IsAttached) OutputCassette = null;
        if (OutputCassette && OutputCassette.gameObject.activeInHierarchy) return false;
        issued.RemoveAll(cassette => !cassette);
        int loose = 0;
        foreach (var cassette in issued)
            if (cassette.gameObject.activeInHierarchy && !cassette.GetComponent<Pickupable>().IsAttached) loose++;
        if (loose >= maxLooseCassettes) return false;

        var instance = Instantiate(cassettePrefab, feedStart.position, feedStart.rotation);
        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        feeding = instance.GetComponent<Cassette>();
        if (clip) feeding.Clip = clip;
        feedingBody = instance.GetComponent<Rigidbody>();
        feedingBody.interpolation = RigidbodyInterpolation.None;
        feedingBody.isKinematic = true;
        feedingColliders = instance.GetComponentsInChildren<Collider>(true);
        colliderStates = new bool[feedingColliders.Length];
        for (int i = 0; i < feedingColliders.Length; i++)
        {
            colliderStates[i] = feedingColliders[i].enabled;
            feedingColliders[i].enabled = false;
        }
        SetPickupTarget(false);
        issued.Add(feeding);
        LastSpawnedCassette = feeding;
        timer = spawnInterval;
        progress = 0f;
        MoveCassette(0f);
        if (spawnAudio && spawnAudio.clip) spawnAudio.Play();
        return true;
    }

    private void MoveCassette(float amount)
    {
        feedingBody.position = Vector3.Lerp(feedStart.position, outputPose.position, amount);
        feedingBody.rotation = Quaternion.Slerp(feedStart.rotation, outputPose.rotation, amount);
    }

    private void CompleteFeed()
    {
        if (!feeding || !feedingBody) { feeding = null; return; }
        MoveCassette(1f);
        for (int i = 0; i < feedingColliders.Length; i++)
            if (feedingColliders[i]) feedingColliders[i].enabled = colliderStates[i];
        OutputCassette = feeding;
        feeding = null;
        feedingBody = null;
        feedingColliders = null;
        colliderStates = null;
        SetPickupTarget(true);
        CassetteReady?.Invoke(OutputCassette);
    }

    public string GetPrompt(PlayerInteractor interactor) => "[E] Take cassette";
    public bool CanInteract(PlayerInteractor interactor) => isActiveAndEnabled && !InTransit
        && OutputCassette && !OutputCassette.GetComponent<Pickupable>().IsAttached && !interactor.HeldItem;
    public void Interact(PlayerInteractor interactor)
    {
        if (!CanInteract(interactor)) return;
        var item = OutputCassette.GetComponent<Pickupable>();
        OutputCassette = null;
        SetPickupTarget(false);
        timer = spawnInterval;
        interactor.Hold(item);
    }

    private void SetPickupTarget(bool value) { if (pickupTarget) pickupTarget.enabled = value; }
    private void OnDisable() { if (feeding) CompleteFeed(); SetPickupTarget(false); }

    private void OnDrawGizmosSelected()
    {
        if (!feedStart || !outputPose) return;
        Gizmos.color = new Color(.4f, 1f, .6f);
        Gizmos.DrawLine(feedStart.position, outputPose.position);
        Gizmos.DrawWireSphere(outputPose.position, .08f);
    }
}
