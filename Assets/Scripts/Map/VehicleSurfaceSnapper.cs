using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class VehicleSurfaceSnapper : MonoBehaviour
{
    [SerializeField] float _verticalOffset = 0.8f;
    [SerializeField] float _snapSpeed = 10f;
    [SerializeField] bool _enable = true;

    Rigidbody _rb;
    TerrainStreamController _controller;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _controller = FindFirstObjectByType<TerrainStreamController>();
    }

    void FixedUpdate()
    {
        if (!_enable || _rb == null) return;
        Vector3 pos = _rb.position;
        float hm = _controller != null ? _controller.HeightMultiplier : 12f;
        float ground = NoiseProvider.GetHeight(pos.x, pos.z, hm);
        float desiredY = ground + _verticalOffset;
        if (pos.y < desiredY)
        {
            Vector3 newPos = Vector3.Lerp(pos, new Vector3(pos.x, desiredY, pos.z), Mathf.Clamp01(_snapSpeed * Time.fixedDeltaTime));
            _rb.MovePosition(newPos);
            var v = _rb.linearVelocity;
            if (v.y < 0) { v.y = 0; _rb.linearVelocity = v; }
        }
    }
}
