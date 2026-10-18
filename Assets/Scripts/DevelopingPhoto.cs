using UnityEngine;

/// <summary>One print, one exposure and its own development progress, even after it is taken down.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Pickupable))]
public sealed class DevelopingPhoto : MonoBehaviour
{
    [SerializeField] private Renderer photoRenderer;
    [SerializeField] private Texture2D[] anomalyImages;
    [SerializeField] private Texture2D[] normalImages;
    [SerializeField, Min(.1f)] private float developmentSeconds = 10f;

    private static readonly int ImageId = Shader.PropertyToID("_Image");
    private static readonly int DevelopmentId = Shader.PropertyToID("_Development");
    private MaterialPropertyBlock properties;
    private float elapsed;

    public Pickupable Item { get; private set; }
    public Texture2D Image { get; private set; }
    public bool IsAnomaly { get; private set; }
    public bool IsExposed => Image != null;
    public float Progress => Mathf.Clamp01(elapsed / developmentSeconds);
    public bool IsDeveloped => Progress >= 1f;
    public PhotoClothespin HangingFrom { get; private set; }
    public bool HasImages => assignedImage || HasImage(anomalyImages) || HasImage(normalImages);

    // Картинка с перфокарты: если задана, увеличитель засветит именно её, а не случайную.
    private Texture2D assignedImage;
    private bool assignedIsAnomaly;

    /// <summary>Принтер вызывает при печати по карте (кнопка EXTRACT).</summary>
    public void AssignImage(Texture2D image, bool isAnomaly)
    {
        assignedImage = image;
        assignedIsAnomaly = isAnomaly;
    }

    private void Awake()
    {
        Item = GetComponent<Pickupable>();
        properties = new MaterialPropertyBlock();
        ApplyAppearance();
    }

    private void Update()
    {
        if (!IsExposed || !HangingFrom || IsDeveloped) return;
        elapsed = Mathf.Min(elapsed + Time.deltaTime, developmentSeconds);
        ApplyAppearance();
    }

    public bool Expose()
    {
        if (IsExposed || !HasImages) return false;

        if (assignedImage)
        {
            Image = assignedImage;
            IsAnomaly = assignedIsAnomaly;
            ApplyAppearance();
            return true;
        }

        bool anomaliesAvailable = HasImage(anomalyImages);
        bool normalsAvailable = HasImage(normalImages);
        IsAnomaly = anomaliesAvailable && (!normalsAvailable || Random.value < .5f);
        var images = IsAnomaly ? anomalyImages : normalImages;
        // Ignore unassigned entries in the Inspector without changing the category probability.
        int count = 0;
        foreach (var image in images) if (image) count++;
        int choice = Random.Range(0, count);
        foreach (var image in images)
            if (image && choice-- == 0) { Image = image; break; }
        ApplyAppearance();
        return true;
    }

    public void HangOn(PhotoClothespin clothespin) => HangingFrom = clothespin;
    public void TakeDown() => HangingFrom = null;

    private void ApplyAppearance()
    {
        if (!photoRenderer) return;
        photoRenderer.GetPropertyBlock(properties);
        if (Image) properties.SetTexture(ImageId, Image);
        properties.SetFloat(DevelopmentId, Progress);
        photoRenderer.SetPropertyBlock(properties);
    }

    private static bool HasImage(Texture2D[] images)
    {
        if (images == null) return false;
        foreach (var image in images) if (image) return true;
        return false;
    }
}