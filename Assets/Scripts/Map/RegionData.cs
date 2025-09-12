using UnityEngine;

[CreateAssetMenu(menuName = "Map/RegionData", fileName = "RegionData")]
public class RegionData : ScriptableObject
{
    [System.Serializable]
    public struct Region
    {
        public string name;
        [Range(0f, 1f)]
        public float startHeight;
        [Range(0f, 1f)]
        public float blend;
        public Color color;
    }

    public Region[] regions;

    public static Color EvaluateColor(Region[] r, float normalizedHeight)
    {
        if (r == null || r.Length == 0) return Color.white;
        Color last = r[0].color;
        foreach (var region in r)
        {
            if (normalizedHeight >= region.startHeight) last = region.color;
            else break;
        }
        return last;
    }
}
