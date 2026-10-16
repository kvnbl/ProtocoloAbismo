using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemigoBasico : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMaxima = 3;

    [Header("Jugador")]
    public Transform jugador;

    [Header("Movimiento")]
    public float velocidad = 1.5f;
    public float distanciaDeteccion = 8f;
    public float distanciaDetencion = 1.5f;

    [Header("Aspecto")]
    public SpriteRenderer imagen;

    private Rigidbody2D rb;
    private int vidaActual;
    private bool muerto;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        vidaActual = vidaMaxima;

        if (imagen == null)
            imagen = GetComponentInChildren<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        if (muerto)
            return;

        float movimientoX = 0f;

        if (jugador != null)
        {
            Vector2 diferencia =
                (Vector2)jugador.position - rb.position;

            bool estaCerca =
                diferencia.magnitude <= distanciaDeteccion;

            if (estaCerca)
            {
                // El dibujo original mira hacia la derecha.
                if (imagen != null &&
                    Mathf.Abs(diferencia.x) > 0.05f)
                {
                    imagen.flipX = diferencia.x < 0f;
                }

                if (Mathf.Abs(diferencia.x) >
                    distanciaDetencion)
                {
                    movimientoX =
                        Mathf.Sign(diferencia.x) * velocidad;
                }
            }
        }

        // Conserva la velocidad vertical para mantener la gravedad.
        rb.linearVelocity = new Vector2(
            movimientoX,
            rb.linearVelocity.y
        );
    }

    public void RecibirDanio(int cantidad)
    {
        if (muerto || cantidad <= 0)
            return;

        vidaActual = Mathf.Max(0, vidaActual - cantidad);

        Debug.Log(
            name + ": quedan " + vidaActual + " vidas.",
            this
        );

        if (vidaActual == 0)
        {
            muerto = true;
            rb.linearVelocity = Vector2.zero;

            // Luego sustituiremos esto por la animación de muerte.
            Destroy(gameObject);
        }
    }
}