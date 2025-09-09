// RegionData.cs
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Map/RegionData", fileName = "RegionData")]
public class RegionData : ScriptableObject
{
    [Serializable]
    public struct Region
    {
        public string name;
        [Range(0f, 1f)] public float startHeight; // 0..1
        [Range(0f, 1f)] public float blend;       // how wide the transition to the next region is (0..1)
        [Range(0f, 1f)] public float tintStrength;// how much to mix 'tint' vs 'texture' (we use color only here)
        public Color tint;
    }

    public Region[] regions;

    // Ensure regions are sorted ascending by startHeight (editor can do this manually,
    // but helper below keeps Evaluate simple)
    static void EnsureSorted(Region[] r)
    {
        if (r == null || r.Length <= 1) return;
        Array.Sort(r, (a, b) => a.startHeight.CompareTo(b.startHeight));
    }

    // Evaluate color with blending between adjacent regions.
    // - regions: can be null
    // - normalizedHeight: 0..1
    public static Color EvaluateColor(Region[] regions, float normalizedHeight)
    {
        if (regions == null || regions.Length == 0) return Color.white;

        // make a local copy and sort to be safe (cheap for a few regions)
        var r = (Region[])regions.Clone();
        EnsureSorted(r);

        // if below the first region
        if (normalizedHeight <= r[0].startHeight) return r[0].tint;

        // if above the last region
        if (normalizedHeight >= r[r.Length - 1].startHeight) return r[r.Length - 1].tint;

        // find the lower region index
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

        // apply region A blend strength: smaller blend => sharper border; larger blend => softer
        float blendRange = Mathf.Clamp01(Mathf.Max(A.blend, B.blend));
        if (blendRange <= 0f)
        {
            // hard step
            return t < 0.5f ? A.tint : B.tint;
        }

        // scale t inside blendRange window centered on boundary
        // map t(0..1) through a smoothstep and limit by blendRange
        float softT = Mathf.SmoothStep(0f, 1f, t);
        // Mix using blendRange influence:
        // If blendRange small -> closer to step; if large -> more smooth
        float mix = Mathf.Clamp01(softT * (1f / Mathf.Clamp(blendRange, 0.001f, 1f)));

        // final color lerp
        Color mixed = Color.Lerp(A.tint, B.tint, mix);

        // optionally apply tintStrength: but since we're only using tint color here,
        // treat tintStrength as intensity (darker when less)
        float intensity = Mathf.Lerp(1f, Mathf.Clamp01(A.tintStrength), 1f - mix);
        return Color.Lerp(Color.Lerp(A.tint, B.tint, mix), mixed * intensity, 0.5f);
    }
}
