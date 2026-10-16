using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProyectilCanon : MonoBehaviour
{
    [Header("Impacto")]
    public LayerMask capasSuelo;

    [Header("Tiempo máximo en escena")]
    public float tiempoVida = 8f;

    private Rigidbody2D rb;
    private Collider2D miCollider;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        miCollider = GetComponent<Collider2D>();
    }

    public void Lanzar(
        Vector2 velocidadInicial,
        Transform propietario
    )
    {
        // Evita que choque con la excavadora que lo disparó.
        if (propietario != null)
        {
            Collider2D[] collidersVehiculo =
                propietario.GetComponentsInChildren<Collider2D>();

            foreach (Collider2D otro in collidersVehiculo)
            {
                if (otro != miCollider)
                {
                    Physics2D.IgnoreCollision(miCollider, otro);
                }
            }
        }

        // Se establece una sola vez.
        // Después, la gravedad modifica la trayectoria.
        rb.linearVelocity = velocidadInicial;

        float angulo = Mathf.Atan2(
            velocidadInicial.y,
            velocidadInicial.x
        ) * Mathf.Rad2Deg;

        rb.rotation = angulo;

        Destroy(gameObject, tiempoVida);
    }

    private void FixedUpdate()
    {
        Vector2 velocidad = rb.linearVelocity;

        if (velocidad.sqrMagnitude > 0.001f)
        {
            float angulo = Mathf.Atan2(
                velocidad.y,
                velocidad.x
            ) * Mathf.Rad2Deg;

            // La punta sigue la dirección de movimiento.
            rb.MoveRotation(angulo);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ComprobarSuelo(collision.gameObject.layer);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        ComprobarSuelo(otro.gameObject.layer);
    }

    private void ComprobarSuelo(int capa)
    {
        if ((capasSuelo.value & (1 << capa)) != 0)
        {
            Destroy(gameObject);
        }
    }
}