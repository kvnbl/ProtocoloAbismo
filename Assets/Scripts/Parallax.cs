using UnityEngine;

// Se ejecuta después de tu ControlCamara.
[DefaultExecutionOrder(100)]
public class Parallax : MonoBehaviour
{
    [Header("Cámara")]
    public Transform camara;

    [Header("Profundidad")]
    [Range(0f, 1f)]
    public float factorParallax = 0.8f;

    private Vector3 posicionInicial;
    private float inicioCamaraX;

    private void Start()
    {
        if (camara == null && Camera.main != null)
            camara = Camera.main.transform;

        if (camara == null)
        {
            Debug.LogWarning(
                "Asigna la cámara en ParallaxFondo.",
                this
            );

            enabled = false;
            return;
        }

        posicionInicial = transform.position;
        inicioCamaraX = camara.position.x;
    }

    private void LateUpdate()
    {
        if (camara == null)
            return;

        float desplazamientoCamara =
            camara.position.x - inicioCamaraX;

        transform.position = new Vector3(
            posicionInicial.x +
                desplazamientoCamara * factorParallax,
            posicionInicial.y,
            posicionInicial.z
        );
    }
}