using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class AplastamientoExcavadora : MonoBehaviour
{
    [Header("Aplastamiento con las orugas")]
    public Collider2D colliderPies;
    public LayerMask capasEnemigos;
    public LayerMask capasSuelo;

    [Min(0.1f)]
    public float velocidadMinimaCaida = 1f;

    [Min(0f)]
    public float margenDeteccion = 0.04f;

    [Min(0f)]
    public float toleranciaCabeza = 0.08f;

    [Header("Taladro solido: no cuenta como aplastamiento")]
    public Collider2D colliderTaladro;

    [Min(0.01f)]
    public float margenPasoVertical = 0.15f;

    private Rigidbody2D rb;
    private float velocidadAntesDeFisica;
    private float ultimoDescenso = float.NegativeInfinity;

    private readonly RaycastHit2D[] resultados =
        new RaycastHit2D[32];

    private readonly HashSet<Collider2D> ignorados =
        new HashSet<Collider2D>();

    private readonly List<Collider2D> retirar =
        new List<Collider2D>(32);

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;

        ExcavadoraController controlador =
            GetComponent<ExcavadoraController>();

        if (capasSuelo.value == 0 && controlador != null)
        {
            capasSuelo = controlador.groundLayer;
        }
    }

    private void Start()
    {
        if (!ColliderPropioValido(colliderPies))
        {
            Debug.LogError(
                "Aplastamiento: asigna el collider solido " +
                "de las orugas, perteneciente al mismo Rigidbody2D.",
                this
            );
        }

        if (!ColliderPropioValido(colliderTaladro))
        {
            Debug.LogError(
                "Aplastamiento: asigna el collider SOLIDO " +
                "del taladro, perteneciente al mismo Rigidbody2D. " +
                "No uses PuntaDanio.",
                this
            );
        }

        if (capasEnemigos.value == 0)
        {
            Debug.LogError(
                "Aplastamiento: configura Capas Enemigos.",
                this
            );
        }
    }

    private static bool ColliderActivo(Collider2D cuerpo)
    {
        return cuerpo != null &&
               cuerpo.enabled &&
               !cuerpo.isTrigger &&
               cuerpo.gameObject.activeInHierarchy;
    }

    private bool ColliderPropioValido(Collider2D cuerpo)
    {
        return ColliderActivo(cuerpo) &&
               cuerpo.attachedRigidbody == rb;
    }

    private bool PuedeAplastar()
    {
        return isActiveAndEnabled &&
               ColliderPropioValido(colliderPies) &&
               velocidadAntesDeFisica <=
                   -Mathf.Max(0.1f, velocidadMinimaCaida);
    }

    private void FixedUpdate()
    {
        velocidadAntesDeFisica = rb.linearVelocity.y;

        if (velocidadAntesDeFisica < -0.1f)
        {
            ultimoDescenso = Time.time;
        }

        ActualizarColisionTaladro();

        if (!PuedeAplastar())
            return;

        ContactFilter2D filtro = new ContactFilter2D();

        filtro.SetLayerMask(
            capasEnemigos.value | capasSuelo.value
        );

        filtro.useTriggers = false;

        float alcance =
            -velocidadAntesDeFisica * Time.fixedDeltaTime +
            Mathf.Max(0f, margenDeteccion);

        int cantidad = colliderPies.Cast(
            Vector2.down,
            filtro,
            resultados,
            alcance,
            true
        );

        // Busca el piso para no aplastar a través de plataformas.
        float distanciaSuelo = float.PositiveInfinity;

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit2D golpe = resultados[i];

            if (golpe.collider == null ||
                golpe.normal.y < 0.65f)
            {
                continue;
            }

            int capa = 1 << golpe.collider.gameObject.layer;

            if ((capasSuelo.value & capa) != 0)
            {
                distanciaSuelo = Mathf.Min(
                    distanciaSuelo,
                    golpe.distance
                );
            }
        }

        // Puede matar varios básicos en una misma caída.
        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit2D golpe = resultados[i];

            if (golpe.collider == null ||
                golpe.normal.y < 0.65f ||
                golpe.distance >= distanciaSuelo)
            {
                continue;
            }

            int capa = 1 << golpe.collider.gameObject.layer;

            if ((capasEnemigos.value & capa) == 0)
                continue;

            bool vieneDesdeArriba =
                colliderPies.bounds.min.y >=
                golpe.collider.bounds.max.y - toleranciaCabeza;

            if (!vieneDesdeArriba)
                continue;

            EnemigoBasico enemigo =
                golpe.collider.GetComponentInParent<EnemigoBasico>();

            if (enemigo != null && enemigo.EstaVivo)
            {
                enemigo.MorirPorAplastamiento();
            }
        }

        // No cambia la velocidad ni aplica rebote.
    }

    // Este método recibe la colisión desde el enemigo.
    public bool IntentarDesdeContacto(
        EnemigoBasico enemigo,
        Collision2D colision)
    {
        if (!PuedeAplastar() ||
            enemigo == null ||
            !enemigo.EstaVivo ||
            colision.collider != colliderPies ||
            colision.otherCollider == null)
        {
            return false;
        }

        int capaEnemigo =
            1 << colision.otherCollider.gameObject.layer;

        if ((capasEnemigos.value & capaEnemigo) == 0)
            return false;

        for (int i = 0; i < colision.contactCount; i++)
        {
            ContactPoint2D contacto =
                colision.GetContact(i);

            bool desdeArriba =
                contacto.enabled &&
                contacto.normal.y <= -0.65f &&
                colliderPies.bounds.center.y >
                    colision.otherCollider.bounds.center.y;

            if (desdeArriba)
            {
                enemigo.MorirPorAplastamiento();
                return true;
            }
        }

        return false;
    }

    private bool PasoVerticalSobre(Collider2D enemigo)
    {
        if (!ColliderPropioValido(colliderTaladro) ||
            !ColliderActivo(enemigo) ||
            rb.linearVelocity.y > 0.1f ||
            Time.time - ultimoDescenso > 0.15f)
        {
            return false;
        }

        Bounds taladro = colliderTaladro.bounds;
        Bounds objetivo = enemigo.bounds;

        float alcance =
            Mathf.Max(0f, -rb.linearVelocity.y) *
            Time.fixedDeltaTime +
            Mathf.Max(0.01f, margenPasoVertical);

        bool coincideHorizontalmente =
            taladro.max.x > objetivo.min.x &&
            taladro.min.x < objetivo.max.x;

        bool estaEncima =
            taladro.min.y >=
            objetivo.max.y - toleranciaCabeza;

        bool estaCerca =
            taladro.min.y - objetivo.max.y <= alcance;

        return coincideHorizontalmente &&
               estaEncima &&
               estaCerca;
    }

    private void ActualizarColisionTaladro()
    {
        retirar.Clear();

        foreach (Collider2D enemigo in ignorados)
        {
            if (!ColliderPropioValido(colliderTaladro) ||
                !ColliderActivo(enemigo))
            {
                RestaurarColision(enemigo);
                retirar.Add(enemigo);
                continue;
            }

            // Reaplica el estado si se reactivó un collider.
            if (!Physics2D.GetIgnoreCollision(
                colliderTaladro,
                enemigo))
            {
                Physics2D.IgnoreCollision(
                    colliderTaladro,
                    enemigo,
                    true
                );
            }

            ColliderDistance2D distancia =
                colliderTaladro.Distance(enemigo);

            bool yaSeparados =
                !distancia.isOverlapped &&
                distancia.distance > 0.02f;

            if (yaSeparados && !PasoVerticalSobre(enemigo))
            {
                RestaurarColision(enemigo);
                retirar.Add(enemigo);
            }
        }

        foreach (Collider2D enemigo in retirar)
        {
            ignorados.Remove(enemigo);
        }

        if (!ColliderPropioValido(colliderTaladro) ||
            rb.linearVelocity.y > 0.1f ||
            Time.time - ultimoDescenso > 0.15f)
        {
            return;
        }

        ContactFilter2D filtro = new ContactFilter2D();

        filtro.SetLayerMask(capasEnemigos);
        filtro.useTriggers = false;

        float alcance =
            Mathf.Max(0f, -rb.linearVelocity.y) *
            Time.fixedDeltaTime +
            Mathf.Max(0.01f, margenPasoVertical);

        int cantidad = colliderTaladro.Cast(
            Vector2.down,
            filtro,
            resultados,
            alcance,
            true
        );

        for (int i = 0; i < cantidad; i++)
        {
            Collider2D cuerpo = resultados[i].collider;

            if (cuerpo == null ||
                ignorados.Contains(cuerpo) ||
                !PasoVerticalSobre(cuerpo))
            {
                continue;
            }

            EnemigoBasico enemigo =
                cuerpo.GetComponentInParent<EnemigoBasico>();

            if (enemigo == null ||
                !enemigo.EstaVivo ||
                Physics2D.GetIgnoreCollision(
                    colliderTaladro,
                    cuerpo))
            {
                continue;
            }

            // Solo ignora este par durante la pasada vertical.
            Physics2D.IgnoreCollision(
                colliderTaladro,
                cuerpo,
                true
            );

            ignorados.Add(cuerpo);
        }
    }

    private void RestaurarColision(Collider2D enemigo)
    {
        if (ColliderActivo(colliderTaladro) &&
            ColliderActivo(enemigo))
        {
            Physics2D.IgnoreCollision(
                colliderTaladro,
                enemigo,
                false
            );
        }
    }

    private void OnDisable()
    {
        foreach (Collider2D enemigo in ignorados)
        {
            RestaurarColision(enemigo);
        }

        ignorados.Clear();
        retirar.Clear();
        ultimoDescenso = float.NegativeInfinity;
    }
}