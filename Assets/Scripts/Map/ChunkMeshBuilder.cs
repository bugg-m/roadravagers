using System;
using UnityEngine;

public static class ChunkMeshBuilder
{
    public struct MeshBuildResult
    {
        public int vertsPerSide;
        public float[] heights;      // vs*vs
        public float[] normalized;   // vs*vs
        public int[] triangles;      // grid triangles
        public float step;
    }

    public static MeshBuildResult Build(int chunkSize, Vector2Int coord, int vertsPerSide, int octaves,
                                        float noiseScale, float persistence, float lacunarity,
                                        Vector2[] octaveOffsets, float maxPossibleAmp,
                                        float heightMultiplier, int quantizeBands)
    {
        int vs = Math.Max(3, vertsPerSide);
        int vertCount = vs * vs;
        float[] heights = new float[vertCount];
        float[] normalized = new float[vertCount];

        float step = (float)chunkSize / (vs - 1);
        float chunkWx = coord.x * (float)chunkSize;
        float chunkWz = coord.y * (float)chunkSize;

        int i = 0;
        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                float worldX = chunkWx + x * step;
                float worldZ = chunkWz + z * step;

                float total = 0f;
                float amplitude = 1f;
                float frequency = 1f;
                for (int o = 0; o < octaveOffsets.Length; o++)
                {
                    float sx = worldX * noiseScale * frequency + octaveOffsets[o].x;
                    float sz = worldZ * noiseScale * frequency + octaveOffsets[o].y;
                    float p = Mathf.PerlinNoise(sx, sz) * 2f - 1f;
                    total += p * amplitude;
                    amplitude *= persistence;
                    frequency *= lacunarity;
                }

                float norm = (total / maxPossibleAmp) * 0.5f + 0.5f;
                norm = Mathf.Clamp01(norm);

                if (quantizeBands > 1)
                {
                    float q = Mathf.Round(norm * quantizeBands) / (float)quantizeBands;
                    norm = Mathf.Clamp01(q);
                }

                normalized[i] = norm;
                heights[i] = norm * heightMultiplier;
                i++;
            }
        }

        int quadCount = (vs - 1) * (vs - 1);
        int[] triangles = new int[quadCount * 6];
        int ti = 0;
        for (int z = 0; z < vs - 1; z++)
        {
            for (int x = 0; x < vs - 1; x++)
            {
                int a = z * vs + x;
                int b = z * vs + x + 1;
                int c = (z + 1) * vs + x;
                int d = (z + 1) * vs + x + 1;

                triangles[ti++] = a; triangles[ti++] = c; triangles[ti++] = b;
                triangles[ti++] = b; triangles[ti++] = c; triangles[ti++] = d;
            }
        }

        return new MeshBuildResult { vertsPerSide = vs, heights = heights, normalized = normalized, triangles = triangles, step = step };
    }
}
