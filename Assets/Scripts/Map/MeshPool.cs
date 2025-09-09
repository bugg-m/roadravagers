using System.Collections.Generic;
using UnityEngine;

public static class MeshPool
{
    static readonly Stack<Mesh> s_pool = new Stack<Mesh>();

    public static Mesh Get()
    {
        if (s_pool.Count > 0) return s_pool.Pop();
        return new Mesh();
    }

    public static void Release(Mesh m)
    {
        if (m == null) return;
        m.Clear();
        s_pool.Push(m);
    }
}
