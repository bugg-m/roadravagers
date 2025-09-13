using System.Collections;
using UnityEngine;

public class ParticleAutoRelease : MonoBehaviour
{
    private ParticleVfxPool _pool;
    private Coroutine _running;

    public void Setup(ParticleVfxPool pool, float life)
    {
        _pool = pool;
        if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(WaitAndRelease(life));
    }

    private IEnumerator WaitAndRelease(float lifeSeconds)
    {
        if (lifeSeconds <= 0f)
        {
            float fallback = 0f;
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in systems)
            {
                try
                {
                    var main = ps.main;
                    float dur = main.duration;
                    if (main.loop) dur = Mathf.Max(dur, 1f);
                    fallback = Mathf.Max(fallback, dur);
                }
                catch { /* ignore */ }
            }
            lifeSeconds = Mathf.Max(0.1f, fallback);
        }

        yield return new WaitForSeconds(lifeSeconds);

        if (_pool != null)
        {
            _pool.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
    }
}
