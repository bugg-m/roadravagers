using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    readonly GameObject _prefab;
    readonly Transform _parent;
    readonly Queue<GameObject> _items = new Queue<GameObject>();

    public GameObjectPool(GameObject prefab, int initialSize = 0, Transform parent = null)
    {
        _prefab = prefab;
        _parent = parent;
        for (int i = 0; i < initialSize; i++)
            Release(CreateNew());
    }

    GameObject CreateNew()
    {
        var go = Object.Instantiate(_prefab, _parent);
        go.SetActive(false);
        return go;
    }

    public GameObject Get()
    {
        var go = _items.Count > 0 ? _items.Dequeue() : CreateNew();
        go.SetActive(true);
        return go;
    }

    public GameObject Get(Vector3 worldPos, Quaternion rotation)
    {
        var go = Get();
        go.transform.SetParent(null, true);
        go.transform.position = worldPos;
        go.transform.rotation = rotation;
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;
        go.transform.SetParent(_parent, true);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.SetActive(false);
        _items.Enqueue(go);
    }

    public void Clear()
    {
        while (_items.Count > 0) Object.Destroy(_items.Dequeue());
    }
}
