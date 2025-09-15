using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    readonly GameObject _prefab;
    readonly Transform _inactiveParent;
    readonly Queue<GameObject> _items = new Queue<GameObject>();

    public GameObjectPool(GameObject prefab, int initialSize = 0, Transform inactiveParent = null)
    {
        _prefab = prefab;
        _inactiveParent = inactiveParent;

        for (int i = 0; i < initialSize; i++)
            Release(CreateNew());
    }

    GameObject CreateNew()
    {
        if (_prefab == null)
        {
            var placeholder = new GameObject("pool_placeholder");
            TryParent(placeholder.transform, _inactiveParent);
            placeholder.SetActive(false);
            return placeholder;
        }

        bool parentIsScene = _inactiveParent != null && _inactiveParent.gameObject.scene.IsValid();

        GameObject go;
        if (parentIsScene)
            go = Object.Instantiate(_prefab, _inactiveParent, false);
        else
            go = Object.Instantiate(_prefab);

        go.name = _prefab.name + "_p";
        go.SetActive(false);

        if (!parentIsScene)
            TryParent(go.transform, _inactiveParent);

        return go;
    }

    static void TryParent(Transform child, Transform parent)
    {
        if (parent == null) return;
        if (parent.gameObject.scene.IsValid())
            child.SetParent(parent, true);
    }

    public GameObject Get()
    {
        var go = _items.Count > 0 ? _items.Dequeue() : CreateNew();
        go.SetActive(true);
        return go;
    }

    public GameObject Get(Vector3 worldPos, Quaternion rotation, Transform overrideActiveParent = null)
    {
        var go = Get();

        if (overrideActiveParent != null && overrideActiveParent.gameObject.scene.IsValid())
            go.transform.SetParent(overrideActiveParent, true);
        else
            go.transform.SetParent(null, true);

        go.transform.position = worldPos;
        go.transform.rotation = rotation;
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        if (_inactiveParent != null && _inactiveParent.gameObject.scene.IsValid())
        {
            go.transform.SetParent(_inactiveParent, true);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
        }
        else
        {
            go.transform.SetParent(null, true);
        }

        go.SetActive(false);
        _items.Enqueue(go);
    }

    public void Clear()
    {
        while (_items.Count > 0)
            Object.Destroy(_items.Dequeue());
    }
}
