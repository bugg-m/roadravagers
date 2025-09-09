using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ThreadedDataRequester : MonoBehaviour
{
    static ThreadedDataRequester s_instance;
    readonly Queue<ThreadResult> _results = new Queue<ThreadResult>();

    void Awake()
    {
        if (s_instance == null) s_instance = this;
        else if (s_instance != this) Destroy(gameObject);
    }

    public static void RequestData<T>(Func<T> generateData, Action<T> callback)
    {
        if (s_instance == null)
        {
            var go = new GameObject("ThreadedDataRequester");
            s_instance = go.AddComponent<ThreadedDataRequester>();
            DontDestroyOnLoad(go);
        }

        var threadStart = new ThreadStart(() =>
        {
            var data = generateData();
            lock (s_instance._results)
            {
                s_instance._results.Enqueue(new ThreadResult { callback = o => callback((T)o), parameter = data });
            }
        });
        new Thread(threadStart).Start();
    }

    void Update()
    {
        lock (_results)
        {
            while (_results.Count > 0)
            {
                var r = _results.Dequeue();
                r.callback(r.parameter);
            }
        }
    }

    struct ThreadResult { public Action<object> callback; public object parameter; }
}
