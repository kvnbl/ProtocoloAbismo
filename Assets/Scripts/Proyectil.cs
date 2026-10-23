using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Proyectil : MonoBehaviour
{
    [Min(0)] public int danio = 1;
    public LayerMask capasImpacto = ~0;
    [Min(0.1f)] public float tiempoVida = 3f;

    [Header("Impacto contra escenario")]
    public LayerMask capasEscenario = ~0;
    public Texture2D hojaImpactoEscenario;
    [Tooltip("Ancho de la celda del efecto en unidades del mundo.")]
    [Min(0.05f)] public float tamanoImpacto = 1f;
    [Min(1f)] public float cuadrosImpactoPorSegundo = 24f;
    [Tooltip("Opcional: material propio del efecto. Vacio: material por defecto de SpriteRenderer.")]
    public Material materialImpacto;
    public int ordenImpacto = 20;

    [Header("Gusano")]
    public bool mostrarReboteCaparazon = true;

    private Rigidbody2D rb;
    private Collider2D miCollider;
    private SpriteRenderer imagenBala;
    private Vector2 posicionAnterior;
    private bool impacto;
    private readonly RaycastHit2D[] contactos = new RaycastHit2D[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        miCollider = GetComponent<Collider2D>();
        imagenBala = GetComponentInChildren<SpriteRenderer>();
        if (miCollider == null)
        {
            Debug.LogError("Proyectil necesita un Collider2D en el mismo objeto.", this);
            enabled = false;
            Destroy(gameObject);
            return;
        }
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        posicionAnterior = rb.position;
        Destroy(gameObject, Mathf.Max(0.1f, tiempoVida));
    }

    private void FixedUpdate() { posicionAnterior = rb.position; }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!PuedeImpactar(otro)) return;
        Vector2 desplazamiento = rb.position - posicionAnterior;
        Vector2 direccion = desplazamiento.sqrMagnitude > 0.000001f
            ? desplazamiento.normalized : rb.linearVelocity.normalized;
        if (direccion.sqrMagnitude < 0.001f) direccion = transform.right;
        Vector2 punto = otro.ClosestPoint(posicionAnterior);
        Vector2 normal = posicionAnterior - punto;
        normal = normal.sqrMagnitude > 0.000001f
            ? normal.normalized : -direccion;
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasImpacto);
        filtro.useTriggers = true;
        float radio = miCollider.bounds.extents.magnitude;
        int cantidad = Physics2D.Raycast(posicionAnterior, direccion, filtro,
            contactos, desplazamiento.magnitude + radio + 0.02f);
        float distancia = float.PositiveInfinity;
        for (int i = 0; i < cantidad; i++)
        {
            if (PuedeImpactar(contactos[i].collider) &&
                contactos[i].distance < distancia)
            {
                distancia = contactos[i].distance;
                otro = contactos[i].collider;
                punto = contactos[i].point;
                normal = contactos[i].normal;
            }
        }
        Golpear(otro, punto, -direccion, normal);
    }

    private void OnCollisionEnter2D(Collision2D colision)
    {
        if (!PuedeImpactar(colision.collider)) return;
        Vector2 salida = (posicionAnterior - rb.position).normalized;
        if (salida.sqrMagnitude < 0.001f) salida = -(Vector2)transform.right;
        Vector2 punto = colision.contactCount > 0
            ? colision.GetContact(0).point : colision.collider.ClosestPoint(posicionAnterior);
        Vector2 normal = colision.contactCount > 0
            ? colision.GetContact(0).normal : salida;
        Golpear(colision.collider, punto, salida, normal);
    }

    private bool PuedeImpactar(Collider2D otro)
    {
        if (impacto || otro == null || otro == miCollider ||
            otro.attachedRigidbody == rb ||
            otro.GetComponentInParent<ExcavadoraController>() != null ||
            (capasImpacto.value & (1 << otro.gameObject.layer)) == 0)
            return false;
        ZonaDanioGusano zona = otro.GetComponent<ZonaDanioGusano>();
        if (zona != null)
            return zona.EstaActiva;
        return !otro.isTrigger;
    }

    private void Golpear(Collider2D otro, Vector2 punto, Vector2 salida, Vector2 normal)
    {
        impacto = true;
        miCollider.enabled = false;
        rb.simulated = false;
        ZonaDanioGusano zona = otro.GetComponent<ZonaDanioGusano>();
        EnemigoBasico enemigo = otro.GetComponentInParent<EnemigoBasico>();
        if (zona != null)
        {
            if (zona.Parte == ParteGusano.Caparazon)
            {
                if (mostrarReboteCaparazon)
                    ReboteBalaGusano.Crear(imagenBala, punto, -salida, normal);
                CrearImpactoEscenario(punto, normal);
            }
            else
            {
                zona.RecibirDanio(danio, TipoAtaqueGusano.Bala);
            }
        }
        else if (enemigo != null)
        {
            if (enemigo.EstaVivo)
                enemigo.RecibirDanioBala(danio, punto, salida);
        }
        else if ((capasEscenario.value & (1 << otro.gameObject.layer)) != 0 &&
                 hojaImpactoEscenario != null)
        {
            CrearImpactoEscenario(punto, normal);
        }
        Destroy(gameObject);
    }

    private void CrearImpactoEscenario(Vector2 punto, Vector2 normal)
    {
        if (hojaImpactoEscenario == null)
            return;
        ImpactoEscenarioBala.Crear(
            hojaImpactoEscenario, punto, normal,
            tamanoImpacto, cuadrosImpactoPorSegundo,
            imagenBala != null ? imagenBala.sortingLayerID : 0,
            ordenImpacto, gameObject.layer, transform.position.z, materialImpacto);
    }
}
