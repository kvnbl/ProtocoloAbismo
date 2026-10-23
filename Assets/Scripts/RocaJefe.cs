using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class RocaJefe : MonoBehaviour
{
    [Header("Caida")]
    [Min(0.1f)] public float gravedad = 18f;
    [Min(0.1f)] public float velocidadMaximaCaida = 10f;
    [Min(0.1f)] public float tiempoVida = 8f;
    public float giroPorSegundo = 60f;

    [Header("Impacto")]
    [Min(1)] public int danio = 1;
    [Range(0.1f, 0.5f)] public float radioRelativo = 0.38f;

    private Rigidbody2D rb;
    private CircleCollider2D zona;
    private SpriteRenderer imagen;
    private VidaJugador jugador;
    private TaladroTermico taladro;
    private LayerMask capasSuelo;
    private Texture2D hojaEfectos;
    private Vector2 velocidad;
    private float edad;
    private bool preparada;
    private bool consumida;
    private readonly List<RaycastHit2D> impactos = new List<RaycastHit2D>(24);

    public SpriteRenderer Imagen => GetComponent<SpriteRenderer>();

    public float RadioEstimado(Sprite variante, float multiplicadorEscala)
    {
        Sprite sprite = variante != null ? variante : Imagen.sprite;
        if (sprite == null)
            return 0.5f;
        float lado = Mathf.Max(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit;
        Vector3 escala = transform.lossyScale;
        return lado * Mathf.Clamp(radioRelativo, 0.1f, 0.5f) *
            Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.y)) * multiplicadorEscala;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        zona = GetComponent<CircleCollider2D>();
        imagen = GetComponent<SpriteRenderer>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        zona.isTrigger = true;
        zona.offset = Vector2.zero;
    }

    public void Inicializar(Vector2 velocidadInicial, VidaJugador objetivo,
        TaladroTermico punta, LayerMask suelo, Texture2D efectos, Sprite variante)
    {
        jugador = objetivo;
        taladro = punta;
        capasSuelo = suelo;
        hojaEfectos = efectos;
        velocidad = velocidadInicial;
        if (variante != null)
            imagen.sprite = variante;
        if (imagen.sprite == null)
        {
            Debug.LogError("RocaJefe: falta el sprite del prefab.", this);
            Destroy(gameObject);
            return;
        }
        Sprite sprite = imagen.sprite;
        zona.radius = Mathf.Max(sprite.rect.width, sprite.rect.height) /
            sprite.pixelsPerUnit * Mathf.Clamp(radioRelativo, 0.1f, 0.5f);
        edad = 0f;
        preparada = true;
    }

    private bool PuntaActiva => taladro != null && taladro.PuedeTaladrar &&
        taladro.zonaDanio != null && taladro.zonaDanio.enabled &&
        taladro.zonaDanio.gameObject.activeInHierarchy;

    private bool TocaTaladroActivo()
    {
        if (!PuntaActiva)
            return false;
        ColliderDistance2D distancia = Physics2D.Distance(zona, taladro.zonaDanio);
        return distancia.isValid && distancia.isOverlapped;
    }

    private void FixedUpdate()
    {
        if (!preparada || consumida)
            return;
        edad += Time.fixedDeltaTime;
        if (edad >= tiempoVida || jugador == null)
        {
            Destroy(gameObject);
            return;
        }
        if (TocaTaladroActivo())
        {
            Romper(rb.position);
            return;
        }

        velocidad.y = Mathf.Max(-Mathf.Max(0.1f, velocidadMaximaCaida),
            velocidad.y - Mathf.Max(0.1f, gravedad) * Time.fixedDeltaTime);
        Vector2 paso = velocidad * Time.fixedDeltaTime;
        if (paso.sqrMagnitude > 0.000001f)
        {
            ContactFilter2D filtro = new ContactFilter2D();
            filtro.useTriggers = true;
            impactos.Clear();
            zona.Cast(paso.normalized, filtro, impactos, paso.magnitude);
            int elegido = -1;
            float menor = float.PositiveInfinity;
            bool elegidoEsPunta = false;
            for (int i = 0; i < impactos.Count; i++)
            {
                Collider2D otro = impactos[i].collider;
                if (!EsImpactoValido(otro))
                    continue;
                bool esPunta = PuntaActiva && otro == taladro.zonaDanio;
                float distancia = impactos[i].distance;
                // Si ambos contactos coinciden, el taladro protege primero.
                if (distancia < menor - 0.001f ||
                    (Mathf.Abs(distancia - menor) <= 0.001f && esPunta && !elegidoEsPunta))
                {
                    elegido = i;
                    menor = distancia;
                    elegidoEsPunta = esPunta;
                }
            }
            if (elegido >= 0)
            {
                Vector2 punto = rb.position + paso.normalized * Mathf.Max(0f, menor - 0.01f);
                AplicarImpacto(impactos[elegido].collider, punto);
                return;
            }
        }
        rb.MovePosition(rb.position + paso);
        rb.MoveRotation(rb.rotation + giroPorSegundo * Time.fixedDeltaTime);
    }

    private bool EsImpactoValido(Collider2D otro)
    {
        if (otro == null || otro == zona || !otro.enabled)
            return false;
        if (PuntaActiva && otro == taladro.zonaDanio)
            return true;
        if (otro.isTrigger)
            return false;
        return otro.GetComponentInParent<VidaJugador>() == jugador ||
            (capasSuelo.value & (1 << otro.gameObject.layer)) != 0;
    }

    private void OnTriggerEnter2D(Collider2D otro) => IntentarImpacto(otro);
    private void OnTriggerStay2D(Collider2D otro) => IntentarImpacto(otro);

    private void IntentarImpacto(Collider2D otro)
    {
        if (!preparada || consumida)
            return;
        if (TocaTaladroActivo())
        {
            Romper(rb.position);
            return;
        }
        if (EsImpactoValido(otro))
            AplicarImpacto(otro, rb.position);
    }

    private void AplicarImpacto(Collider2D otro, Vector2 punto)
    {
        if (consumida)
            return;
        bool bloqueada = PuntaActiva && otro == taladro.zonaDanio;
        bool golpeoJugador = otro.GetComponentInParent<VidaJugador>() == jugador;
        // Se consume ANTES del dano, incluso si el jugador es invulnerable.
        Romper(punto);
        if (!bloqueada && golpeoJugador && jugador != null)
            jugador.RecibirDanio(Mathf.Max(1, danio));
    }

    private void Romper(Vector2 punto)
    {
        if (consumida)
            return;
        consumida = true;
        zona.enabled = false;
        imagen.enabled = false;
        float ancho = RadioEstimado(imagen.sprite, 1f) * 3.2f;
        EfectoRocaJefe.Crear(hojaEfectos, 2,
            new Vector3(punto.x, punto.y, transform.position.z), ancho, imagen, 0.3f, false);
        Destroy(gameObject);
    }
}
