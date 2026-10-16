using UnityEngine;

public class Proyectil : MonoBehaviour
{
    [Header("Daño por bala")]
    public int danio = 1;

    private bool impactoProcesado;
    private Camera camaraJuego;

    private void Awake()
    {
        camaraJuego = Camera.main;
    }

    private bool EstaDentroDePantalla()
    {
        if (camaraJuego == null)
            return true;

        Vector3 posicion =
            camaraJuego.WorldToViewportPoint(transform.position);

        return posicion.z > 0f &&
               posicion.x >= 0f && posicion.x <= 1f &&
               posicion.y >= 0f && posicion.y <= 1f;
    }

    private void LateUpdate()
    {
        if (!EstaDentroDePantalla())
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (impactoProcesado)
            return;

        // Ignora la excavadora y sus partes.
        if (otro.GetComponentInParent<ExcavadoraController>() != null)
            return;

        // Ignora zonas de detección, como AreaTaladro.
        if (otro.isTrigger)
            return;

        // Evita dañar enemigos fuera de la pantalla.
        if (!EstaDentroDePantalla())
        {
            impactoProcesado = true;
            Destroy(gameObject);
            return;
        }

        impactoProcesado = true;

        EnemigoBasico enemigo =
            otro.GetComponentInParent<EnemigoBasico>();

        if (enemigo != null)
        {
            enemigo.RecibirDanio(danio);
        }

        // Se destruye al tocar un enemigo o una superficie sólida.
        Destroy(gameObject);
    }
}