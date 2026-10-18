using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Guides a card through the pipe, then releases it into the room with normal pickup physics.</summary>
public sealed class PunchCardTubeSpawner : MonoBehaviour
{
    [Header("Cards / issued in order")]
    [Tooltip("Содержимое карт по порядку. Если список заполнен, труба выдаёт эти карты, создавая их из Card Model Prefab.\nНовая карточка = новый CardData, перетащенный сюда.")]
    [SerializeField] private CardData[] cardDataList;
    [Tooltip("Модель карты для списка выше (например, Normal_punchcard). Пусто — берётся первый префаб из Card Prefabs.")]
    [SerializeField] private GameObject cardModelPrefab;
    [Tooltip("Старый способ: готовые префабы карт. Используется, только если Card Data List пуст.")]
    [SerializeField] private GameObject[] cardPrefabs;
    [SerializeField, Min(0)] private float initialDelay = 2f;
    [Tooltip("Вкл — выдавать список по кругу бесконечно. Выкл — выдать каждую карту из списка по одному разу и остановиться.")]
    [SerializeField] private bool repeat = true;
    [SerializeField, Min(.1f)] private float spawnInterval = 10f;
    [SerializeField, Min(1)] private int maxLooseCards = 3;

    [Header("Pipe centreline")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform bendStart;
    [SerializeField] private Transform bendEnd;
    [SerializeField] private Transform releasePoint;
    [SerializeField, Min(.01f)] private float bendHandle = .44f;
    [SerializeField, Min(.1f)] private float travelSpeed = 3.5f;

    [Header("Exit")]
    [SerializeField, Min(0)] private float exitSpeed = 1.1f;
    [SerializeField] private float downwardSpeed = .15f;
    [SerializeField] private float tumbleSpeed = 2.2f;

    public bool InTransit => transit != null;
    // A scene-local tutorial can request one card while keeping the route and pickup physics running.
    public bool AutomaticSpawning { get; set; } = true;

    private bool UsesDataList => cardDataList != null && cardDataList.Length > 0;
    private int CardCount => UsesDataList ? cardDataList.Length : (cardPrefabs != null ? cardPrefabs.Length : 0);
    private GameObject ModelPrefab => cardModelPrefab ? cardModelPrefab
        : (cardPrefabs != null && cardPrefabs.Length > 0 ? cardPrefabs[0] : null);
    public Pickupable LastSpawnedCard { get; private set; }
    public event Action<Pickupable> CardReleased;

    private sealed class Transit
    {
        public Pickupable item;
        public Rigidbody body;
        public Collider[] colliders;
        public bool[] enabled;
        public Vector3 centreOffset;
        public float distance;
    }
    private readonly List<Pickupable> issued = new List<Pickupable>();
    private readonly List<Vector3> positions = new List<Vector3>();
    private readonly List<Quaternion> rotations = new List<Quaternion>();
    private readonly List<float> distances = new List<float>();
    private Transit transit;
    private Vector3 exitDirection, widthDirection;
    private float timer;
    private bool ready;
    private int issuedCount;
    private int nextPrefab;

    private void Start()
    {
        ready = spawnPoint && bendStart && bendEnd && releasePoint && CardCount > 0 && (!UsesDataList || ModelPrefab);
        if (!ready) { Debug.LogError("PunchCardTubeSpawner needs cards (Card Data List + model, or Card Prefabs) and four route points.", this); return; }
        BuildPath();
        timer = initialDelay;
    }

    private void Update()
    {
        if (!ready || !AutomaticSpawning || !repeat && issuedCount >= CardCount) return;
        timer -= Time.deltaTime;
        if (timer > 0) return;
        TrySpawnNext();
    }

    public bool TrySpawnNext()
    {
        if (!Application.isPlaying || !ready || transit != null) return false;
        if (CardCount == 0) return false;
        issued.RemoveAll(item => !item);
        int loose = 0;
        foreach (var item in issued)
            if (item.gameObject.activeInHierarchy && !item.IsAttached) loose++;
        if (loose >= maxLooseCards) return false;
        nextPrefab %= CardCount;
        GameObject prefab = UsesDataList ? ModelPrefab : cardPrefabs[nextPrefab];
        if (!prefab || !prefab.GetComponent<Pickupable>() || !prefab.GetComponent<Rigidbody>())
        {
            Debug.LogError("A tube card prefab needs Pickupable and Rigidbody.", this);
            timer = spawnInterval;
            return false;
        }
        BuildPath();
        var instance = Instantiate(prefab, positions[0], rotations[0]);
        SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
        if (UsesDataList)
        {
            if (instance.TryGetComponent(out PunchCard punchCard)) punchCard.Data = cardDataList[nextPrefab];
            else Debug.LogError("Card Model Prefab has no PunchCard component.", this);
        }
        var itemComponent = instance.GetComponent<Pickupable>();
        var body = instance.GetComponent<Rigidbody>();
        var cardColliders = instance.GetComponentsInChildren<Collider>(true);
        var enabled = new bool[cardColliders.Length];
        Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
        {
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        Vector3 centre = hasBounds ? instance.transform.InverseTransformPoint(bounds.center) : Vector3.zero;
        transit = new Transit
        {
            item = itemComponent,
            body = body,
            colliders = cardColliders,
            enabled = enabled,
            centreOffset = Vector3.Scale(centre, instance.transform.localScale)
        };
        for (int i = 0; i < cardColliders.Length; i++) { enabled[i] = cardColliders[i].enabled; cardColliders[i].enabled = false; }
        body.isKinematic = true; body.useGravity = false; body.interpolation = RigidbodyInterpolation.Interpolate;
        body.position = positions[0] - rotations[0] * transit.centreOffset; body.rotation = rotations[0];
        issued.Add(itemComponent); LastSpawnedCard = itemComponent;
        nextPrefab = (nextPrefab + 1) % CardCount;
        issuedCount++; timer = spawnInterval;
        return true;
    }

    private void FixedUpdate()
    {
        if (transit == null) return;
        if (!transit.item || !transit.body) { transit = null; return; }
        transit.distance += travelSpeed * Time.fixedDeltaTime;
        if (transit.distance >= distances[distances.Count - 1]) { Release(); return; }
        int segment = 0;
        while (segment + 1 < distances.Count - 1 && distances[segment + 1] < transit.distance) segment++;
        float t = Mathf.InverseLerp(distances[segment], distances[segment + 1], transit.distance);
        Vector3 position = Vector3.Lerp(positions[segment], positions[segment + 1], t);
        Quaternion rotation = Quaternion.Slerp(rotations[segment], rotations[segment + 1], t);
        transit.body.MovePosition(position - rotation * transit.centreOffset);
        transit.body.MoveRotation(rotation);
    }

    private void Release()
    {
        var card = transit; transit = null;
        if (card == null || !card.body) return;
        int last = positions.Count - 1;
        card.body.position = positions[last] - rotations[last] * card.centreOffset;
        card.body.rotation = rotations[last];
        card.body.isKinematic = false; card.body.useGravity = true;
        card.body.mass = .03f; card.body.linearDamping = .2f; card.body.angularDamping = .7f;
        card.body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        for (int i = 0; i < card.colliders.Length; i++)
            if (card.colliders[i]) card.colliders[i].enabled = card.enabled[i];
        card.body.linearVelocity = exitDirection * exitSpeed + Vector3.down * downwardSpeed;
        card.body.angularVelocity = widthDirection * tumbleSpeed;
        CardReleased?.Invoke(card.item);
    }

    private void OnDisable() => Release();

    private void BuildPath()
    {
        positions.Clear(); rotations.Clear(); distances.Clear();
        Vector3 incoming = (bendStart.position - spawnPoint.position).normalized;
        exitDirection = (releasePoint.position - bendEnd.position).normalized;
        widthDirection = Vector3.Cross(incoming, exitDirection).normalized;
        if (widthDirection.sqrMagnitude < .01f) widthDirection = spawnPoint.right;
        AddPoint(spawnPoint.position, incoming);
        AddPoint(bendStart.position, incoming);
        Vector3 a = bendStart.position, b = a + incoming * bendHandle;
        Vector3 d = bendEnd.position, c = d - exitDirection * bendHandle;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f, u = 1 - t;
            Vector3 point = u * u * u * a + 3 * u * u * t * b + 3 * u * t * t * c + t * t * t * d;
            Vector3 direction = (3 * u * u * (b - a) + 6 * u * t * (c - b) + 3 * t * t * (d - c)).normalized;
            AddPoint(point, direction);
        }
        AddPoint(releasePoint.position, exitDirection);
    }
    private void AddPoint(Vector3 point, Vector3 direction)
    {
        float distance = positions.Count == 0 ? 0 : distances[distances.Count - 1] + Vector3.Distance(positions[positions.Count - 1], point);
        positions.Add(point); distances.Add(distance);
        // Card width stays across the pipe; its face follows the bend without turning into the walls.
        rotations.Add(Quaternion.LookRotation(Vector3.Cross(widthDirection, direction), direction));
    }

    [ContextMenu("Spawn Next Card")]
    private void SpawnFromInspector() => TrySpawnNext();

    private void OnDrawGizmosSelected()
    {
        if (!spawnPoint || !bendStart || !bendEnd || !releasePoint) return;
        Gizmos.color = new Color(.25f, 1, .45f);
        Gizmos.DrawLine(spawnPoint.position, bendStart.position);
        Gizmos.DrawLine(bendStart.position, bendEnd.position);
        Gizmos.DrawLine(bendEnd.position, releasePoint.position);
        Gizmos.DrawWireSphere(spawnPoint.position, .08f); Gizmos.DrawWireSphere(releasePoint.position, .08f);
    }
}