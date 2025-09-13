using UnityEngine;
using System.Collections.Generic;

public class ParticleVfxPool : MonoBehaviour
{
    [Header("Prefab & pool")]
    [SerializeField] private GameObject prefab;

    [SerializeField, Min(0)] private int initialSize = 4;

    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    private Transform runtimeParent;

    void Awake()
    {
        runtimeParent = (gameObject.scene.IsValid()) ? transform : VfxPoolManager.Instance.transform;

        if (!gameObject.scene.IsValid())
        {
            var go = new GameObject(gameObject.name + "_RuntimePool");
            go.transform.SetParent(VfxPoolManager.Instance.transform, false);
            runtimeParent = go.transform;
        }

        for (int i = 0; i < initialSize; i++)
        {
            var inst = CreateNew();
            ReleaseInternal(inst);
        }
    }

    public GameObject Spawn(Vector3 pos, Quaternion rot, float lifeSeconds)
    {
        GameObject instance;
        if (pool.Count > 0)
        {
            instance = pool.Dequeue();
            if (instance == null)
            {
                instance = CreateNew();
            }
        }
        else
        {
            instance = CreateNew();
        }

        instance.transform.SetParent(null);
        instance.transform.position = pos;
        instance.transform.rotation = rot;
        instance.SetActive(true);

        var auto = instance.GetComponent<ParticleAutoRelease>();
        if (auto == null) auto = instance.AddComponent<ParticleAutoRelease>();
        auto.Setup(this, lifeSeconds);

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Clear(true);
            ps.Play(true);
        }

        return instance;
    }

    public void Release(GameObject instance)
    {
        if (instance == null) return;
        ReleaseInternal(instance);
    }

    internal void ReleaseInternal(GameObject instance)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        instance.SetActive(false);

        if (runtimeParent != null)
        {
            instance.transform.SetParent(runtimeParent, false);
        }
        else
        {
            instance.transform.SetParent(VfxPoolManager.Instance.transform, false);
        }

        pool.Enqueue(instance);
    }

    private GameObject CreateNew()
    {
        if (prefab == null)
        {
            var empty = new GameObject(name + "_empty");
            empty.transform.SetParent(runtimeParent, false);
            return empty;
        }

        GameObject instance = Instantiate(prefab);
        instance.name = prefab.name + "_inst";
        instance.transform.SetParent(runtimeParent, false);
        instance.SetActive(false);

        if (instance.GetComponent<ParticleAutoRelease>() == null)
            instance.AddComponent<ParticleAutoRelease>();

        return instance;
    }
}
