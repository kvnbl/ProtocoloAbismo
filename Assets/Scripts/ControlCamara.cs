using UnityEngine;

public class ControlCamara : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform objetivo; // Arrastra a tu jugador (la excavadora) aquí

    [Header("Configuración")]
    public float offsetX = 3f; // Desplazamiento en X (para que el jugador no esté pegado al borde izquierdo)
    public float suavizado = 5f; // Velocidad con la que la cámara sigue al jugador

    private float maxX; // Almacena la posición máxima que ha alcanzado la cámara en el eje X

    void Start()
    {
        if (objetivo != null)
        {
            // Inicializa la posición máxima con la posición actual de la cámara al iniciar el nivel
            maxX = transform.position.x;
        }
    }

    void LateUpdate()
    {
        if (objetivo != null)
        {
            // Calcula dónde debería estar la cámara basándose en el jugador
            float posicionDeseadaX = objetivo.position.x + offsetX;

            // Si el jugador avanzó más allá del máximo registrado, actualizamos el límite
            if (posicionDeseadaX > maxX)
            {
                maxX = posicionDeseadaX;
            }

            // Creamos el vector hacia donde debe ir la cámara (congelando su propia altura Y y profundidad Z)
            Vector3 posicionDestino = new Vector3(maxX, transform.position.y, transform.position.z);

            // Movemos la cámara suavemente hacia la derecha
            transform.position = Vector3.Lerp(transform.position, posicionDestino, suavizado * Time.deltaTime);
        }
    }
}