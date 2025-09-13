using System;
using UnityEngine;

/// <summary>
/// Pure numeric mesh builder — safe to call from background threads.
/// Produces vertices/heights/triangles/normalized + baked normals.
/// </summary>
public static class ChunkMeshBuilder
{
    public struct MeshBuildResult
    {
        public int vertsPerSide;
        public float[] heights;      // length = vs*vs, row-major (z*vs + x)
        public float[] normalized;   // length = vs*vs
        public int[] triangles;      // triangle indices for the grid (indices into verts)
        public float step;           // grid spacing
        public Vector3[] normals;    // per-vertex normals (baked numerically)
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

                // FBM
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

        // Bake normals numerically (sum triangle contributions, then normalize).
        Vector3[] verts = new Vector3[vertCount];
        for (int z = 0; z < vs; z++)
        {
            for (int x = 0; x < vs; x++)
            {
                int idx = z * vs + x;
                verts[idx] = new Vector3(x * step, heights[idx], z * step);
            }
        }

        Vector3[] normals = new Vector3[vertCount];
        for (int t = 0; t < triangles.Length; t += 3)
        {
            int ia = triangles[t];
            int ib = triangles[t + 1];
            int ic = triangles[t + 2];

            Vector3 pa = verts[ia];
            Vector3 pb = verts[ib];
            Vector3 pc = verts[ic];

            Vector3 triNormal = Vector3.Cross(pb - pa, pc - pa);
            // don't normalize here; accumulate then normalize per-vertex
            normals[ia] += triNormal;
            normals[ib] += triNormal;
            normals[ic] += triNormal;
        }

        for (int n = 0; n < normals.Length; n++)
        {
            Vector3 nn = normals[n];
            if (nn.sqrMagnitude > 1e-9f)
                normals[n] = nn.normalized;
            else
                normals[n] = Vector3.up;
        }

        return new MeshBuildResult
        {
            vertsPerSide = vs,
            heights = heights,
            normalized = normalized,
            triangles = triangles,
            step = step,
            normals = normals
        };
    }
}
