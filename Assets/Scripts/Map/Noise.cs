using UnityEngine;

public static class Noise
{
    public static int octaves = 4;
    public static float persistence = 0.5f;
    public static float lacunarity = 2f;
    public static float noiseScale = 0.006f;
    public static Vector2[] octaveOffsets;
    public static float maxPossibleAmplitude = 1f;
    public static System.Random prng;

    public static void Initialize(int seed, int octavesIn, float persistenceIn, float lacunarityIn, float noiseScaleIn, Vector2 globalOffset)
    {
        octaves = Mathf.Max(1, octavesIn);
        persistence = Mathf.Clamp(persistenceIn, 0.01f, 1f);
        lacunarity = Mathf.Max(1f, lacunarityIn);
        noiseScale = Mathf.Max(0.00001f, noiseScaleIn);
        prng = new System.Random(seed);
        octaveOffsets = new Vector2[octaves];

        for (int i = 0; i < octaves; i++)
        {
            float ox = prng.Next(-100000, 100000) + globalOffset.x;
            float oy = prng.Next(-100000, 100000) + globalOffset.y;
            octaveOffsets[i] = new Vector2(ox, oy);
        }

        float amp = 1f;
        maxPossibleAmplitude = 0f;
        for (int i = 0; i < octaves; i++)
        {
            maxPossibleAmplitude += amp;
            amp *= persistence;
        }
    }

    public static float GetHeightNormalized(float worldX, float worldZ)
    {
        if (octaveOffsets == null)
        {
            return 0.5f;
        }

        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < octaveOffsets.Length; i++)
        {
            float sx = worldX * noiseScale * frequency + octaveOffsets[i].x;
            float sy = worldZ * noiseScale * frequency + octaveOffsets[i].y;
            float p = Mathf.PerlinNoise(sx, sy) * 2f - 1f;
            total += p * amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        float n = total / maxPossibleAmplitude * 0.5f + 0.5f;
        return Mathf.Clamp01(n);
    }

    public static float GetHeightAtWorldPos(float worldX, float worldZ, float heightMultiplier)
    {
        return GetHeightNormalized(worldX, worldZ) * heightMultiplier;
    }
}