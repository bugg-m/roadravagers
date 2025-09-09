using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/RegionData", fileName = "RegionData")]
public class RegionData : ScriptableObject
{
    [Serializable]
    public struct Region
    {
        public string name;
        [Range(0f, 1f)] public float startHeight;
        [Range(0f, 1f)] public float blend;
        [Range(0f, 1f)] public float tintStrength;
        public Color tint;
    }

    public Region[] regions;

    static void EnsureSorted(Region[] r)
    {
        if (r == null || r.Length <= 1) return;
        Array.Sort(r, (a, b) => a.startHeight.CompareTo(b.startHeight));
    }

    public static Color EvaluateColor(Region[] regions, float normalizedHeight)
    {
        if (regions == null || regions.Length == 0) return Color.white;

        var r = (Region[])regions.Clone();
        EnsureSorted(r);

        if (normalizedHeight <= r[0].startHeight) return r[0].tint;

        if (normalizedHeight >= r[r.Length - 1].startHeight) return r[r.Length - 1].tint;

        int idx = 0;
        for (int i = 0; i < r.Length - 1; i++)
        {
            if (normalizedHeight >= r[i].startHeight && normalizedHeight <= r[i + 1].startHeight)
            {
                idx = i;
                break;
            }
        }

        var A = r[idx];
        var B = r[Mathf.Min(idx + 1, r.Length - 1)];

        float span = Mathf.Max(1e-5f, B.startHeight - A.startHeight);
        float t = Mathf.InverseLerp(A.startHeight, B.startHeight, normalizedHeight);

        float blendRange = Mathf.Clamp01(Mathf.Max(A.blend, B.blend));
        if (blendRange <= 0f)
        {
            return t < 0.5f ? A.tint : B.tint;
        }

        float softT = Mathf.SmoothStep(0f, 1f, t);
        float mix = Mathf.Clamp01(softT * (1f / Mathf.Clamp(blendRange, 0.001f, 1f)));

        Color mixed = Color.Lerp(A.tint, B.tint, mix);

        float intensity = Mathf.Lerp(1f, Mathf.Clamp01(A.tintStrength), 1f - mix);
        return Color.Lerp(Color.Lerp(A.tint, B.tint, mix), mixed * intensity, 0.5f);
    }
}
