using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VFXPool : MonoBehaviour
{
    [SerializeField] private int initialPoolSize = 10;
    [SerializeField] private bool expandPool = true;

    private Dictionary<ParticleSystem, Queue<ParticleSystem>> pools = new Dictionary<ParticleSystem, Queue<ParticleSystem>>();
    private HashSet<ParticleSystem> activeEffects = new HashSet<ParticleSystem>();

    void Awake()
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
    }

    public ParticleSystem PlayEffect(ParticleSystem prefab, Vector3 position, Quaternion rotation, float lifetime = -1f)
    {
        if (prefab == null) return null;

        ParticleSystem instance = GetPooledInstance(prefab);
        if (instance == null) return null;

        instance.transform.SetPositionAndRotation(position, rotation);
        instance.gameObject.SetActive(true);

        ConfigureParticleSystem(instance);
        instance.Play(true);

        activeEffects.Add(instance);

        StartCoroutine(ReturnToPoolAfterTime(instance, lifetime));

        return instance;
    }

    private ParticleSystem GetPooledInstance(ParticleSystem prefab)
    {
        if (!pools.ContainsKey(prefab))
        {
            pools[prefab] = new Queue<ParticleSystem>();
            CreatePoolInstances(prefab, initialPoolSize);
        }

        var pool = pools[prefab];

        if (pool.Count > 0)
        {
            return pool.Dequeue();
        }
        else if (expandPool)
        {
            return CreateInstance(prefab);
        }

        return null;
    }

    private void CreatePoolInstances(ParticleSystem prefab, int count)
    {
        var pool = pools[prefab];

        for (int i = 0; i < count; i++)
        {
            ParticleSystem instance = CreateInstance(prefab);
            pool.Enqueue(instance);
        }
    }

    private ParticleSystem CreateInstance(ParticleSystem prefab)
    {
        GameObject instance = Instantiate(prefab.gameObject, transform);
        instance.name = prefab.name + "_pooled";
        instance.SetActive(false);

        ParticleSystem ps = instance.GetComponent<ParticleSystem>();

        OptimizeParticleSystem(ps);

        return ps;
    }

    private void OptimizeParticleSystem(ParticleSystem ps)
    {
        if (ps == null) return;

        var main = ps.main;

        main.prewarm = false;
        main.cullingMode = ParticleSystemCullingMode.Pause;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        var collision = ps.collision;
        collision.enabled = false;

        var trigger = ps.trigger;
        trigger.enabled = false;

        ParticleSystem[] childSystems = ps.GetComponentsInChildren<ParticleSystem>();
        foreach (var child in childSystems)
        {
            if (child != ps)
            {
                var childMain = child.main;
                childMain.prewarm = false;
                childMain.cullingMode = ParticleSystemCullingMode.Pause;

                var childCollision = child.collision;
                childCollision.enabled = false;

                var childTrigger = child.trigger;
                childTrigger.enabled = false;
            }
        }
    }

    private void ConfigureParticleSystem(ParticleSystem ps)
    {
        ps.Clear(true);

        ps.time = 0f;

        ParticleSystem[] childSystems = ps.GetComponentsInChildren<ParticleSystem>();
        foreach (var child in childSystems)
        {
            child.Clear(true);
            child.time = 0f;
        }
    }

    private IEnumerator ReturnToPoolAfterTime(ParticleSystem instance, float lifetime)
    {
        if (instance == null) yield break;

        float timeElapsed = 0f;

        if (lifetime > 0f)
        {
            while (timeElapsed < lifetime && instance != null && instance.gameObject.activeSelf)
            {
                yield return null;
                timeElapsed += Time.deltaTime;
            }
        }
        else
        {
            float maxDuration = GetMaxDuration(instance);

            while (timeElapsed < maxDuration && instance != null && instance.IsAlive(true))
            {
                yield return null;
                timeElapsed += Time.deltaTime;

                if (timeElapsed > 10f) break;
            }
        }

        ReturnToPool(instance);
    }

    private float GetMaxDuration(ParticleSystem ps)
    {
        float maxDuration = 0f;

        ParticleSystem[] systems = ps.GetComponentsInChildren<ParticleSystem>();
        foreach (var system in systems)
        {
            if (system == null) continue;

            var main = system.main;
            float duration = main.duration;

            if (main.loop)
                duration = 2f;

            maxDuration = Mathf.Max(maxDuration, duration);
        }

        return Mathf.Max(maxDuration, 1f);
    }

    private void ReturnToPool(ParticleSystem instance)
    {
        if (instance == null) return;

        activeEffects.Remove(instance);

        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        instance.Clear(true);

        ParticleSystem[] childSystems = instance.GetComponentsInChildren<ParticleSystem>();
        foreach (var child in childSystems)
        {
            child.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            child.Clear(true);
        }

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.gameObject.SetActive(false);

        foreach (var kvp in pools)
        {
            if (instance.name.Contains(kvp.Key.name))
            {
                kvp.Value.Enqueue(instance);
                break;
            }
        }
    }

    public void ReturnEffect(ParticleSystem instance)
    {
        if (instance != null && activeEffects.Contains(instance))
        {
            StopAllCoroutines();
            ReturnToPool(instance);
        }
    }

    public void ClearAllPools()
    {
        foreach (var effect in activeEffects)
        {
            if (effect != null)
                effect.gameObject.SetActive(false);
        }

        activeEffects.Clear();

        foreach (var pool in pools.Values)
        {
            while (pool.Count > 0)
            {
                var instance = pool.Dequeue();
                if (instance != null)
                    DestroyImmediate(instance.gameObject);
            }
        }

        pools.Clear();
    }

    void OnDestroy()
    {
        ClearAllPools();
    }
}