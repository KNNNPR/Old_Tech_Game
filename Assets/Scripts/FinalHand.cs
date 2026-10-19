using UnityEngine;

/// <summary>
/// Финальный предмет (рука из последнего дня). Вешается на префаб руки рядом с Pickupable.
/// После вставки в компьютер рычаг запускает FinalHandTrigger.
///
/// Ещё задаёт, как рука лежит в руке игрока и в слоте компьютера. Повороты считаются вокруг
/// центра самой модели, а не её точки опоры из Blender, поэтому рука не «уезжает» при повороте.
/// Поля можно крутить прямо в Play — рука меняется сразу. Подобрал — ⋮ → Copy Component,
/// вышел из Play — открой префаб, ⋮ → Paste Component Values.
/// </summary>
[RequireComponent(typeof(Pickupable))]
public class FinalHand : MonoBehaviour, IAttachPose
{
    [Header("В руке игрока")]
    [Tooltip("Поворот руки в руке игрока (градусы).")]
    [SerializeField] private Vector3 handRotation = new Vector3(0f, 180f, 0f);
    [Tooltip("Где центр модели относительно точки руки (метры): X — вправо, Y — вверх, Z — вперёд.")]
    [SerializeField] private Vector3 handPosition = Vector3.zero;

    [Header("В слоте компьютера")]
    [Tooltip("Поворот руки в слоте (градусы, относительно Anchor слота).")]
    [SerializeField] private Vector3 slotRotation = new Vector3(90f, 0f, 0f);
    [Tooltip("Где центр модели относительно Anchor слота (метры).")]
    [SerializeField] private Vector3 slotPosition = Vector3.zero;

    private Pickupable item;
    private PlayerInteractor interactor;
    private bool hasCenter;
    private Vector3 centerLocal;
    private float modelRadius;
    private float inspection, inspectionTurn;

    public void SetInspection(float amount, float turn)
    { inspection = Mathf.Clamp01(amount); inspectionTurn = turn; }

    private void Awake()
    {
        item = GetComponent<Pickupable>();
    }

    public bool TryGetPose(Transform parent, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (!hasCenter)
        {
            centerLocal = ModelCenter(transform);
            Bounds bounds = default; bool any = false;
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            { if(!any){bounds=renderer.bounds;any=true;}else bounds.Encapsulate(renderer.bounds); }
            modelRadius=any?bounds.extents.magnitude:.5f;hasCenter = true;
        }

        bool inHand = interactor != null && interactor.HeldItem == item;
        localRotation = Quaternion.Euler(inHand ? handRotation : slotRotation);
        Vector3 meters = inHand ? handPosition : slotPosition;

        if (inHand && inspection > 0 && interactor.PlayerCamera)
        {
            localRotation = Quaternion.Slerp(localRotation,Quaternion.Euler(78,handRotation.y+inspectionTurn*.4f,-12),inspection);
            float distance=Mathf.Max(1.1f,modelRadius/Mathf.Sin(interactor.PlayerCamera.fieldOfView*Mathf.Deg2Rad*.5f)*1.12f);
            Vector3 inFront = parent.InverseTransformPoint(interactor.PlayerCamera.transform.TransformPoint(new Vector3(.02f,-.12f,distance)));
            Vector3 normal = ToParentUnits(parent,meters);
            Vector3 centre = localRotation * Vector3.Scale(transform.localScale,centerLocal);
            localPosition = Vector3.Lerp(normal,inFront,inspection)-centre;
            return true;
        }

        // Центр модели должен оказаться в нужной точке → точка опоры смещается на повёрнутый центр.
        Vector3 center = localRotation * Vector3.Scale(transform.localScale, centerLocal);
        localPosition = ToParentUnits(parent, meters) - center;
        return true;
    }

    /// <summary>
    /// Центр модели в собственных координатах объекта (по сеткам, а не по коллайдерам).
    /// Не зависит от того, где и как объект сейчас стоит.
    /// </summary>
    public static Vector3 ModelCenter(Transform root)
    {
        bool any = false;
        Bounds bounds = default;

        void Add(Transform owner, Bounds meshBounds)
        {
            Vector3 c = meshBounds.center, e = meshBounds.extents;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = c + new Vector3(
                    (i & 1) == 0 ? -e.x : e.x,
                    (i & 2) == 0 ? -e.y : e.y,
                    (i & 4) == 0 ? -e.z : e.z);
                Vector3 local = root.InverseTransformPoint(owner.TransformPoint(corner));
                if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                else bounds.Encapsulate(local);
            }
        }

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            if (filter.sharedMesh != null) Add(filter.transform, filter.sharedMesh.bounds);
        foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (skinned.sharedMesh != null) Add(skinned.transform, skinned.sharedMesh.bounds);

        return any ? bounds.center : Vector3.zero;
    }

    // Сдвиг в метрах → в единицы родителя (у моделей из Blender масштаб бывает 100).
    private static Vector3 ToParentUnits(Transform parent, Vector3 meters)
    {
        Vector3 scale = parent != null ? parent.lossyScale : Vector3.one;
        return new Vector3(
            meters.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            meters.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            meters.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
    }
}
