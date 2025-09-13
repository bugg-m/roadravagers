using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ThreadedDataRequester : MonoBehaviour
{
    static ThreadedDataRequester s_instance;

    readonly ConcurrentQueue<ThreadResult> _results = new ConcurrentQueue<ThreadResult>();

    [SerializeField, Min(1)] int _maxConcurrentWorkers = 2;
    SemaphoreSlim _semaphore;

    CancellationTokenSource _cts;

    void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (s_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _semaphore = new SemaphoreSlim(Math.Max(1, _maxConcurrentWorkers));
        _cts = new CancellationTokenSource();
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _semaphore?.Dispose();
        if (s_instance == this) s_instance = null;
    }

    public static void RequestData<T>(Func<T> generateData, Action<T> callback)
    {
        if (generateData == null) throw new ArgumentNullException(nameof(generateData));
        if (callback == null) throw new ArgumentNullException(nameof(callback));

        EnsureInstanceExists();

        _ = s_instance.RunWorkAsync(generateData, callback);
    }

    static void EnsureInstanceExists()
    {
        if (s_instance == null)
        {
            var go = new GameObject("ThreadedDataRequester");
            s_instance = go.AddComponent<ThreadedDataRequester>();
            DontDestroyOnLoad(go);
        }
    }

    async Task RunWorkAsync<T>(Func<T> generateData, Action<T> callback)
    {
        await _semaphore.WaitAsync(_cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

        try
        {
            T result = default;
            Exception error = null;

            try
            {
                result = await Task.Run(() => generateData(), _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            _results.Enqueue(new ThreadResult
            {
                callback = obj =>
                {
                    if (error != null)
                    {
                        Debug.LogException(error);
                    }
                    else
                    {
                        callback((T)obj);
                    }
                },
                parameter = result
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }

    void Update()
    {
        while (_results.TryDequeue(out var r))
        {
            try
            {
                r.callback(r.parameter);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }
    }

    struct ThreadResult
    {
        public Action<object> callback;
        public object parameter;
    }
}
