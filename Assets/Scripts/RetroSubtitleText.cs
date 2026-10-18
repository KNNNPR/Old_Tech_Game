using TMPro;
using UnityEngine;

/// <summary>Small stepped phosphor motion, without moving the subtitle panel or its layout.</summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class RetroSubtitleText : MonoBehaviour
{
    [SerializeField, Range(0,4)] private float amplitude = 1.6f;
    [SerializeField, Min(.1f)] private float stepSize = .65f;
    private TMP_Text textComponent;
    private TMP_MeshInfo[] source;
    private bool refresh = true;

    private void OnEnable()
    {
        textComponent = GetComponent<TMP_Text>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        refresh = true;
    }
    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    private void OnTextChanged(Object changed) { if (changed == textComponent) refresh = true; }
    private void LateUpdate()
    {
        if (refresh || source == null)
        {
            textComponent.ForceMeshUpdate();
            source = textComponent.textInfo.CopyMeshInfoVertexData();
            refresh = false;
        }
        var info = textComponent.textInfo;
        for (int i=0;i<info.characterCount;i++)
        {
            var character = info.characterInfo[i];
            if (!character.isVisible) continue;
            int mesh = character.materialReferenceIndex;
            int vertex = character.vertexIndex;
            float phase = Time.unscaledTime * 1.8f - (i / 3) * .42f;
            float rise = Mathf.Round(Mathf.Sin(phase) * amplitude / stepSize) * stepSize;
            float glow = .93f + .07f * Mathf.Sin(Time.unscaledTime * 2.1f - i * .17f);
            for (int corner=0;corner<4;corner++)
            {
                info.meshInfo[mesh].vertices[vertex+corner] = source[mesh].vertices[vertex+corner] + Vector3.up * rise;
                Color color = source[mesh].colors32[vertex+corner];
                color.r *= glow;color.g *= glow;color.b *= glow;
                info.meshInfo[mesh].colors32[vertex+corner] = color;
            }
        }
        textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }
}
