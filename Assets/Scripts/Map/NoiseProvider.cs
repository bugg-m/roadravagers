using System;
using UnityEngine;

public static class NoiseProvider
{
    static int s_seed = 1337;
    static int s_octaves = 4;
    static float s_persistence = 0.5f;
    static float s_lacunarity = 2f;
    static float s_noiseScale = 0.01f;
    static Vector2 s_globalOffset = Vector2.zero;

    static Vector2[] s_octaveOffsets;
    static float s_maxPossibleAmp;
    static System.Random s_prng;
    static bool s_initialized = false;

    public static void Initialize(int seed, int octaves, float persistence, float lacunarity, float noiseScale, Vector2 globalOffset)
    {
        s_seed = seed;
        s_octaves = Math.Max(1, octaves);
        s_persistence = Mathf.Clamp(persistence, 0.01f, 1f);
        s_lacunarity = Math.Max(1f, lacunarity);
        s_noiseScale = Mathf.Max(0.00001f, noiseScale);
        s_globalOffset = globalOffset;

        s_prng = new System.Random(s_seed);
        s_octaveOffsets = new Vector2[s_octaves];
        float amp = 1f;
        s_maxPossibleAmp = 0f;
        for (int i = 0; i < s_octaves; i++)
        {
            float ox = (float)(s_prng.NextDouble() * 200000f - 100000f) + s_globalOffset.x;
            float oy = (float)(s_prng.NextDouble() * 200000f - 100000f) + s_globalOffset.y;
            s_octaveOffsets[i] = new Vector2(ox, oy);
            s_maxPossibleAmp += amp;
            amp *= s_persistence;
        }
        s_initialized = true;
    }

    public static float GetNormalized(float worldX, float worldZ)
    {
        if (!s_initialized)
            Initialize(s_seed, s_octaves, s_persistence, s_lacunarity, s_noiseScale, s_globalOffset);

        float total = 0f;
        float amplitude = 1f;
        float frequency = 1f;

        for (int i = 0; i < s_octaveOffsets.Length; i++)
        {
            float sx = worldX * s_noiseScale * frequency + s_octaveOffsets[i].x;
            float sz = worldZ * s_noiseScale * frequency + s_octaveOffsets[i].y;
            float p = Mathf.PerlinNoise(sx, sz) * 2f - 1f;
            total += p * amplitude;
            amplitude *= s_persistence;
            frequency *= s_lacunarity;
        }

        float normalized = (total / s_maxPossibleAmp) * 0.5f + 0.5f;
        return Mathf.Clamp01(normalized);
    }

    public static float GetHeight(float worldX, float worldZ, float heightMultiplier)
        => GetNormalized(worldX, worldZ) * heightMultiplier;

    // Expose octave offsets & max amp for thread builders (deterministic)
    public static Vector2[] GetOffsets() => s_octaveOffsets;
    public static float GetMaxPossibleAmplitude() => s_maxPossibleAmp;
}
