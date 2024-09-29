using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float _zoomSpeed = 10f;
    [SerializeField] private float _moveSpeed = 10f;

    private Camera _camera;
    private PixelPerfectCamera _pixelPerfectCamera;

    void Start()
    {
        _camera = GetComponent<Camera>();
        _pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        float zoomFactor = _pixelPerfectCamera.assetsPPU / 100f; // Adjust the divisor based on your zoom level scaling
        Vector3 movement = new Vector3(horizontal, vertical, 0) * _moveSpeed * Time.deltaTime * zoomFactor;
        _camera.transform.Translate(movement);

        var zoom = 0;
        if (Input.GetKey(KeyCode.Q))
        {
            zoom = -1;
        }
        else if (Input.GetKey(KeyCode.E))
        {
            zoom = 1;
        }

        _pixelPerfectCamera.assetsPPU += (int)(_zoomSpeed * zoom);
    }
}