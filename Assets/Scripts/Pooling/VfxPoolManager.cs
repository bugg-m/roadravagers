using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class VfxPoolManager : MonoBehaviour
{
    private static VfxPoolManager _instance;
    public static VfxPoolManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<VfxPoolManager>();
                if (_instance == null)
                {
                    var go = new GameObject("VfxPoolManager");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<VfxPoolManager>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
