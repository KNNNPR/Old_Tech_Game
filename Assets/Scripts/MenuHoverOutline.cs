using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Hover copies drawn by MenuHoverOutlineFeature follow the moving lever.</summary>
public class MenuHoverOutline : MonoBehaviour
{
    [SerializeField, HideInInspector] private List<MeshRenderer> outlines = new List<MeshRenderer>();
    [SerializeField, HideInInspector] private bool hovered;

    public void Configure(Material material, params Renderer[] sources)
    {
        if (material == null) return;
        foreach (Renderer source in sources)
        {
            if (source == null || !source.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
            var copy = new GameObject("Hover outline - " + source.name);
            copy.hideFlags = HideFlags.DontSave;
            copy.layer = MenuHoverOutlineFeature.OutlineLayer; // Separate from the interaction proxies on layer 30.
            copy.transform.SetParent(source.transform, false);
            copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = copy.AddComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var properties = new MaterialPropertyBlock();
            properties.SetVector("_OutlineCentre", filter.sharedMesh.bounds.center);
            renderer.SetPropertyBlock(properties);
            renderer.enabled = false;
            outlines.Add(renderer);
        }
    }

    public void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;
        foreach (var renderer in outlines) if (renderer != null) renderer.enabled = value;
    }

    private void OnDisable() => SetHovered(false);

    private void OnDestroy()
    {
        foreach (var renderer in outlines)
        {
            if (renderer == null) continue;
            if (Application.isPlaying) Destroy(renderer.gameObject);
            else DestroyImmediate(renderer.gameObject);
        }
        outlines.Clear();
    }
}
