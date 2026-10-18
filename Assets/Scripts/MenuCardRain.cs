using UnityEngine;

/// <summary>Fixed pool: cards leave below the viewport and reappear above it without allocations.</summary>
public class MenuCardRain : MonoBehaviour
{
    [SerializeField] private Camera menuCamera;
    [SerializeField] private Transform[] cards;
    private float[] speeds, phases;
    private Vector3[] spins;
    public int RecycledCount { get; private set; }

    private void Awake()
    {
        var random = new System.Random(624);
        speeds = new float[cards.Length];
        phases = new float[cards.Length];
        spins = new Vector3[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            speeds[i] = 0.32f + (float)random.NextDouble() * 0.26f;
            phases[i] = (float)random.NextDouble() * 6.28f;
            spins[i] = new Vector3(8f + i % 5 * 2f, 10f + i % 4 * 3f, (i % 2 == 0 ? 1 : -1) * (5f + i % 7));
        }
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
        for (int i = 0; i < cards.Length; i++)
        {
            Transform card = cards[i];
            card.position += new Vector3(Mathf.Sin(Time.unscaledTime * 0.37f + phases[i]) * 0.09f, -speeds[i], 0) * dt;
            card.Rotate(spins[i] * dt, Space.Self);
            Vector3 viewport = menuCamera.WorldToViewportPoint(card.position);
            if (viewport.y < -0.2f || viewport.z < menuCamera.nearClipPlane)
            {
                // Keep the centre lane clear for the menu, with changing left/right positions.
                float lane = i % 2 == 0 ? 0.13f : 0.87f;
                float x = lane + Mathf.Sin(phases[i] + RecycledCount * 1.7f) * 0.12f;
                card.position = menuCamera.ViewportToWorldPoint(new Vector3(x, 1.22f, 12f + i % 4));
                RecycledCount++;
            }
        }
    }
}
