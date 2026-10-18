using System.Linq;
using UnityEngine;

/// <summary>A faint visible contour for loose pickup items, hidden in hands, machines and delivery tubes.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Pickupable))]
public sealed class PickupPulseOutline : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    [SerializeField, ColorUsage(false, true)] private Color outlineColor = new Color(.8f, 1.1f, .9f);
    [SerializeField, Range(.5f, 4f)] private float width = 1.35f;
    [SerializeField, Min(.1f)] private float pulseSpeed = 2f;
    [SerializeField, Min(1)] private float visibleDistance = 8f;

    private Pickupable item;
    private Collider[] colliders;
    private MenuHoverOutline outline;
    private MaterialPropertyBlock properties;
    private Camera playerCamera;
    private float phase;
    public bool IsVisible { get; private set; }

    private void Awake()
    {
        item = GetComponent<Pickupable>();
        colliders = GetComponentsInChildren<Collider>(true);
        if (!outlineMaterial) { enabled = false; return; }
        var sources = GetComponentsInChildren<Renderer>(true)
            .Where(r => r.gameObject.layer != MenuHoverOutlineFeature.OutlineLayer).ToArray();
        outline = gameObject.AddComponent<MenuHoverOutline>();
        outline.Configure(outlineMaterial, sources);
        properties = new MaterialPropertyBlock();
        phase = Random.value * 6.283185f;
    }

    private void LateUpdate()
    {
        if (!playerCamera) playerCamera = Camera.main;
        IsVisible = item && item.enabled && !item.IsAttached && colliders.Any(c => c && c.enabled)
            && playerCamera && (playerCamera.transform.position - transform.position).sqrMagnitude <= visibleDistance * visibleDistance;
        if (!outline) return;
        outline.SetHovered(IsVisible);
        if (!IsVisible) return;
        float t = Time.time * pulseSpeed + phase;
        float brightness = .88f + Mathf.Sin(t * 3.141593f) * .12f + Mathf.Sin(t * 23f) * .025f;
        outline.SetAppearance(outlineColor * brightness, width, properties);
    }

    private void OnDisable()
    {
        IsVisible = false;
        if (outline) outline.SetHovered(false);
    }
}
