using System;
using UnityEngine;

public class TurretPoolManager : MonoBehaviour
{
    [Header("Pool")]
    [SerializeField] private GameObject turretPrefab;
    [SerializeField] private int initialSize = 24;

    [Header("Parents (scene Transforms)")]
    [SerializeField] private Transform poolParent;
    [SerializeField] private Transform defaultActiveParent;

    public event Action<Vector2Int> OnTurretReleased;

    GameObjectPool _pool;

    void Awake()
    {
        if (poolParent == null)
        {
            var go = new GameObject("TurretPool");
            go.transform.SetParent(transform, false);
            poolParent = go.transform;
        }

        _pool = new GameObjectPool(turretPrefab, initialSize, poolParent);
    }

    public GameObject Spawn(Vector3 pos, Quaternion rot, Vector2Int cell, Transform activeParent = null)
    {
        var chosenActiveParent = activeParent ?? defaultActiveParent;
        var go = _pool.Get(pos, rot, chosenActiveParent);

        var pt = go.GetComponent<TurretAI>();
        if (pt != null)
        {
            pt.PoolOwner = this;
            pt.CellCoord = cell;
        }

        return go;
    }

    public void Release(GameObject turret, Vector2Int cell)
    {
        if (turret == null) return;
        _pool.Release(turret);
        OnTurretReleased?.Invoke(cell);
    }

    public void ReleaseFromTurret(GameObject turret, Vector2Int cell)
    {
        Release(turret, cell);
    }

    public void ClearPool() => _pool.Clear();
}
