using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    readonly GameObject _prefab;
    readonly Transform _parent;
    readonly Queue<GameObject> _items = new Queue<GameObject>();

    public GameObjectPool(GameObject prefab, int initialSize = 0, Transform parent = null)
    {
        _prefab = prefab; _parent = parent;
        for (int i = 0; i < initialSize; i++) Release(CreateNew());
    }

    GameObject CreateNew()
    {
        var go = Object.Instantiate(_prefab, _parent);
        go.SetActive(false);
        return go;
    }

    public GameObject Get()
    {
        if (_items.Count > 0)
        {
            var go = _items.Dequeue();
            go.SetActive(true);
            return go;
        }
        var n = CreateNew();
        n.SetActive(true);
        return n;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.SetActive(false);
        _items.Enqueue(go);
    }

    public void Clear()
    {
        while (_items.Count > 0) Object.Destroy(_items.Dequeue());
    }
}
