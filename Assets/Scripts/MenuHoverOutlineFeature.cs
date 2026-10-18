using UnityEngine.Rendering.Universal;

/// <summary>Mask the target first so only its outer contour can receive the hover color.</summary>
public class MenuHoverOutlineFeature : ScriptableRendererFeature
{
    public const int OutlineLayer = 29;
    private RenderObjectsPass mask, outline;

    public override void Create()
    {
        var cameraSettings = new RenderObjects.CustomCameraSettings();
        mask = new RenderObjectsPass("Menu hover silhouette", RenderPassEvent.AfterRenderingSkybox,
            new[] { "MenuHoverMask" }, RenderQueueType.Opaque, 1 << OutlineLayer, cameraSettings);
        outline = new RenderObjectsPass("Menu hover contour", RenderPassEvent.AfterRenderingSkybox,
            new[] { "MenuHoverContour" }, RenderQueueType.Opaque, 1 << OutlineLayer, cameraSettings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (renderingData.cameraData.cameraType == UnityEngine.CameraType.Preview) return;
        renderer.EnqueuePass(mask);
        renderer.EnqueuePass(outline);
    }
}
