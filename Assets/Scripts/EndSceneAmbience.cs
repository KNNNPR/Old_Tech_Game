using UnityEngine;

/// <summary>Scene-local motion for THE_END. Every card moves around its authored pose.</summary>
public sealed class EndSceneAmbience : MonoBehaviour
{
    [SerializeField] private Transform[] giantCards;
    [SerializeField] private Vector3[] cardPivots;
    [SerializeField] private Light screenLight;
    [SerializeField] private Renderer screen;
    [SerializeField] private Renderer sea;
    [SerializeField] private ParticleSystem ash;
    [SerializeField] private Transform viewer;
    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private MaterialPropertyBlock screenBlock, seaBlock;
    private Color phosphor, reflection;
    private float lightIntensity, startedAt;
    private bool initialized;

    private void OnEnable()
    {
        if (giantCards == null || cardPivots == null || giantCards.Length != cardPivots.Length) return;
        restPositions = new Vector3[giantCards.Length];
        restRotations = new Quaternion[giantCards.Length];
        for (int i = 0; i < giantCards.Length; i++)
        {
            if (!giantCards[i]) continue;
            restPositions[i] = giantCards[i].position;
            restRotations[i] = giantCards[i].rotation;
        }
        screenBlock = new MaterialPropertyBlock();
        seaBlock = new MaterialPropertyBlock();
        if (screen && screen.sharedMaterial) phosphor = screen.sharedMaterial.GetColor("_Phosphor");
        if (sea && sea.sharedMaterial) reflection = sea.sharedMaterial.GetColor("_BeaconColor");
        if (screenLight) lightIntensity = screenLight.intensity;
        startedAt = Time.time;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized) return;
        Apply(Time.time - startedAt);
        // The emitter follows the player; existing flakes stay in world space.
        if (ash && viewer)
        {
            Vector3 position = ash.transform.position;
            position.x = viewer.position.x;
            position.z = viewer.position.z;
            ash.transform.position = position;
        }
    }

    public void Apply(float time)
    {
        if (!initialized) return;
        for (int i = 0; i < giantCards.Length; i++)
        {
            var card = giantCards[i];
            if (!card) continue;
            float phase = i * 2.399963f;
            float frequency = 0.095f + (i % 5) * 0.017f;
            float lean = (Mathf.Sin(time * frequency + phase) - Mathf.Sin(phase)) * (0.28f + (i % 3) * 0.055f);
            float crossLean = (Mathf.Sin(time * frequency * 0.73f + phase * 1.3f) - Mathf.Sin(phase * 1.3f)) * 0.19f;
            var tilt = Quaternion.Euler(lean, 0, crossLean);
            card.SetPositionAndRotation(cardPivots[i] + tilt * (restPositions[i] - cardPivots[i]), tilt * restRotations[i]);
        }
        float pulse = 0.94f + Mathf.Sin(time * 2.7f) * 0.045f + Mathf.Sin(time * 19.1f) * 0.015f;
        if (screenLight) screenLight.intensity = lightIntensity * pulse;
        if (screen)
        {
            screen.GetPropertyBlock(screenBlock);
            screenBlock.SetColor("_Phosphor", phosphor * pulse);
            screen.SetPropertyBlock(screenBlock);
        }
        if (sea)
        {
            sea.GetPropertyBlock(seaBlock);
            seaBlock.SetColor("_BeaconColor", reflection * pulse);
            sea.SetPropertyBlock(seaBlock);
        }
    }

    private void OnDisable()
    {
        if (!initialized) return;
        for (int i = 0; i < giantCards.Length; i++)
            if (giantCards[i]) giantCards[i].SetPositionAndRotation(restPositions[i], restRotations[i]);
        if (screenLight) screenLight.intensity = lightIntensity;
        if (screen)
        {
            screen.GetPropertyBlock(screenBlock);
            screenBlock.SetColor("_Phosphor", phosphor);
            screen.SetPropertyBlock(screenBlock);
        }
        if (sea)
        {
            sea.GetPropertyBlock(seaBlock);
            seaBlock.SetColor("_BeaconColor", reflection);
            sea.SetPropertyBlock(seaBlock);
        }
        initialized = false;
    }
}
