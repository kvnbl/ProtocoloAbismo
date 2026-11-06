using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Responsabilidad: Desplaza una roca, comprueba sus impactos y permite romperla con el taladro o con balas.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class RocaJefe : MonoBehaviour
{

    #region Datos, configuracion y estado

    [Header("Caida")]
    [Min(0.1f)] public float gravedad = 18f;

    [Min(0.1f)] public float velocidadMaximaCaida = 10f;

    [Min(0.1f)] public float tiempoVida = 8f;

    public float giroPorSegundo = 60f;

    [Header("Impacto")]
    [Min(1)] public int danio = 1;

    [Range(0.1f, 0.5f)] public float radioRelativo = 0.38f;

    [Tooltip("Margen en unidades del mundo cuando la roca toca casi a la vez el taladro y el chasis.")]
    [Min(0f)] public float margenBloqueoTaladro = 0.18f;

    [Header("Balas que rompen la roca")]
    [Tooltip("Selecciona la capa de las balas de la torreta. No incluyas otros proyectiles.")]
    [SerializeField] private LayerMask capasBalas;

    [Tooltip("Cada bala de la torreta resta un impacto. El taladro activo rompe la roca al tocarla.")]
    [SerializeField, Min(1)] private int disparosParaRomper = 3;

    [Header("Destello al recibir un disparo")]
    [SerializeField] private Shader shaderDestello;

    [SerializeField, Range(0f, 1f)] private float intensidadDestello = 0.55f;

    [SerializeField, Min(0.01f)] private float duracionDestello = 0.1f;

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

    private int disparosRestantes;

    private SpriteRenderer capaDestello;

    private Material materialDestello;

    private Color colorOriginal;

    private float destelloRestante;

    private bool colorTemporal;

    private readonly List<RaycastHit2D> impactos = new List<RaycastHit2D>(24);

    public SpriteRenderer Imagen => GetComponent<SpriteRenderer>();

    private bool PuntaActiva => taladro != null && taladro.PuedeTaladrar &&
        taladro.zonaDanio != null && taladro.zonaDanio.enabled &&
        taladro.zonaDanio.gameObject.activeInHierarchy;

    #endregion

    #region Inicializacion y activacion

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

    #endregion

    #region Actualizacion durante el juego

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

        MoverRoca();
    }

    private void LateUpdate()
    {
        if (destelloRestante <= 0f)
        {
            if (capaDestello != null)
                capaDestello.enabled = false;
            if (colorTemporal)
            {
                imagen.color = colorOriginal;
                colorTemporal = false;
            }
            return;
        }

        MostrarDestello();
        destelloRestante = Mathf.Max(0f, destelloRestante - Time.deltaTime);
    }

    #endregion

    #region Deteccion de contactos

    private void OnTriggerEnter2D(Collider2D otro) => IntentarImpacto(otro);

    private void OnTriggerStay2D(Collider2D otro) => IntentarImpacto(otro);

    #endregion

    #region Acciones disponibles para otros componentes

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
        disparosRestantes = Mathf.Max(1, disparosParaRomper);
        colorOriginal = imagen.color;
        preparada = true;
    }

    public bool RomperConBala(Vector2 punto)
    {
        if (!preparada || consumida)
            return false;

        disparosRestantes--;
        Destellar();
        if (disparosRestantes <= 0)
            Romper(punto, esperarDestello: true);

        return true;
    }

    #endregion

    #region Logica interna

    private bool TocaTaladroActivo(float margen = 0f)
    {
        if (!PuntaActiva)
            return false;
        ColliderDistance2D distancia = Physics2D.Distance(zona, taladro.zonaDanio);
        return distancia.isValid && distancia.distance <= margen;
    }

    private void MoverRoca()
    {
        velocidad.y = Mathf.Max(-Mathf.Max(0.1f, velocidadMaximaCaida),
            velocidad.y - Mathf.Max(0.1f, gravedad) * Time.fixedDeltaTime);
        Vector2 paso = velocidad * Time.fixedDeltaTime;
        if (paso.sqrMagnitude > 0.000001f)
        {
            ContactFilter2D filtro = new ContactFilter2D();
            filtro.useTriggers = true;
            impactos.Clear();
            float alcance = paso.magnitude;
            float margen = PuntaActiva ? Mathf.Max(0f, margenBloqueoTaladro) : 0f;
            zona.Cast(paso.normalized, filtro, impactos, alcance + margen);
            int elegido = -1;
            float menor = float.PositiveInfinity;
            float distanciaPunta = float.PositiveInfinity;
            for (int i = 0; i < impactos.Count; i++)
            {
                Collider2D otro = impactos[i].collider;
                if (!EsImpactoValido(otro))
                    continue;
                bool esPunta = PuntaActiva && otro == taladro.zonaDanio;
                float distancia = impactos[i].distance;
                if (esPunta)
                    distanciaPunta = Mathf.Min(distanciaPunta, distancia);

                if (distancia <= alcance && distancia < menor)
                {
                    elegido = i;
                    menor = distancia;
                }
            }
            if (elegido >= 0)
            {
                Vector2 punto = rb.position + paso.normalized * Mathf.Max(0f, menor - 0.01f);
                Collider2D otro = impactos[elegido].collider;
                // La punta puede quedar apenas detrás del collider sólido del vehículo.
                if (GolpeaJugador(otro) && distanciaPunta <= menor + margen)
                    Romper(punto);
                else
                    AplicarImpacto(otro, punto);
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
        if (ObtenerBala(otro) != null)
            return true;

        if (otro.isTrigger)
            return false;
        return GolpeaJugador(otro) ||
            (capasSuelo.value & (1 << otro.gameObject.layer)) != 0;
    }

    private Rigidbody2D ObtenerBala(Collider2D otro)
    {
        if (otro == null || !otro.enabled)
            return null;

        Rigidbody2D bala = otro.attachedRigidbody;
        if (bala == null || bala == rb)
            return null;

        int capasObjeto = (1 << otro.gameObject.layer) | (1 << bala.gameObject.layer);
        return (capasBalas.value & capasObjeto) != 0 ? bala : null;
    }

    private bool GolpeaJugador(Collider2D otro) =>
        jugador != null && otro.GetComponentInParent<VidaJugador>() == jugador;

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
        Rigidbody2D bala = ObtenerBala(otro);
        if (bala != null)
        {
            if (!RomperConBala(punto))
                return;

            // El disparo se consume en este paso, aunque toque dos rocas a la vez.
            foreach (Collider2D colliderBala in bala.GetComponentsInChildren<Collider2D>())
                if (colliderBala.attachedRigidbody == bala)
                    colliderBala.enabled = false;
            Destroy(bala.gameObject);
            return;
        }

        bool golpeoJugador = GolpeaJugador(otro);
        bool bloqueada = PuntaActiva && (otro == taladro.zonaDanio ||
            (golpeoJugador && TocaTaladroActivo(Mathf.Max(0f, margenBloqueoTaladro))));
        // Se consume ANTES del daño para no dañar dos veces.
        Romper(punto);
        if (!bloqueada && golpeoJugador)
            jugador.RecibirDanio(Mathf.Max(1, danio));
    }

    private void Destellar()
    {
        if (intensidadDestello <= 0f)
            return;

        if (capaDestello == null)
        {
            if (shaderDestello == null)
                shaderDestello = Shader.Find("Juego/DestelloBlancoSprite");

            if (shaderDestello != null && shaderDestello.isSupported)
            {
                GameObject objeto = new GameObject("DestelloBlanco");
                objeto.layer = gameObject.layer;
                objeto.transform.SetParent(imagen.transform, false);
                capaDestello = objeto.AddComponent<SpriteRenderer>();
                materialDestello = new Material(shaderDestello);
                capaDestello.sharedMaterial = materialDestello;
            }
        }

        destelloRestante = Mathf.Max(0.01f, duracionDestello);
        MostrarDestello();
    }

    private void MostrarDestello()
    {
        float alfa = intensidadDestello *
            Mathf.Clamp01(destelloRestante / Mathf.Max(0.01f, duracionDestello));

        if (capaDestello == null)
        {
            // Si falta el shader, el cambio de color sigue indicando el impacto.
            Color aviso = new Color(1f, 0.45f, 0.35f, colorOriginal.a);
            imagen.color = Color.Lerp(colorOriginal, aviso, alfa);
            colorTemporal = true;
            return;
        }

        capaDestello.sprite = imagen.sprite;
        capaDestello.flipX = imagen.flipX;
        capaDestello.flipY = imagen.flipY;
        capaDestello.sortingLayerID = imagen.sortingLayerID;
        capaDestello.sortingOrder = imagen.sortingOrder + 1;
        capaDestello.color = new Color(1f, 1f, 1f, alfa * colorOriginal.a);
        capaDestello.enabled = imagen.enabled;
    }

    private void Romper(Vector2 punto, bool esperarDestello = false)
    {
        if (consumida)
            return;
        consumida = true;
        zona.enabled = false;

        if (esperarDestello && destelloRestante > 0f)
        {
            StartCoroutine(TerminarRoturaTrasDestello(punto));
            return;
        }

        TerminarRotura(punto);
    }

    private IEnumerator TerminarRoturaTrasDestello(Vector2 punto)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, duracionDestello));
        TerminarRotura(punto);
    }

    private void TerminarRotura(Vector2 punto)
    {
        imagen.enabled = false;
        imagen.color = colorOriginal;
        if (capaDestello != null)
            capaDestello.enabled = false;
        float ancho = RadioEstimado(imagen.sprite, 1f) * 3.2f;
        EfectoRocaJefe.Crear(hojaEfectos, 2,
            new Vector3(punto.x, punto.y, transform.position.z), ancho, imagen, 0.3f, false);
        Destroy(gameObject);
    }

    #endregion

    private void OnDestroy()
    {
        if (materialDestello != null)
            Destroy(materialDestello);
    }
}
