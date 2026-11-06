using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

// Responsabilidad: Lanza el proyectil, detecta impactos y aplica la explosion de area.
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProyectilCanon : MonoBehaviour
{

    #region Datos, configuracion y estado

    [Header("Impacto")]
    public LayerMask capasSuelo;

    public LayerMask capasEnemigos;

    [Header("Explosion")]
    [Min(0)] public int danioExplosion = 3;

    [Tooltip("Ancho total del área de daño, en unidades del mundo.")]
    [FormerlySerializedAs("ladoExplosion")]
    [Min(0.01f)] public float anchoExplosion = 4f;

    [Tooltip("Alto total del área de daño, en unidades del mundo.")]
    [Min(0.01f)] public float altoExplosion = 4f;

    [Tooltip("Desplaza la zona de daño en X y en Y, sin mover el efecto visual.")]
    public Vector2 desplazamientoExplosion;

    [Tooltip("Si el impacto es sobre el piso, coloca el borde inferior del área a la altura del impacto o más arriba.")]
    public bool expandirDesdeElPiso = true;

    [Tooltip("Muestra el rectángulo real en Scene durante un segundo al explotar.")]
    public bool mostrarZonaAlExplotar = true;

    public GameObject efectoExplosion;

    [Min(0.1f)] public float duracionEfecto = 2f;

    [Header("Tiempo maximo en escena")]
    [Min(0.1f)] public float tiempoVida = 8f;

    private Rigidbody2D rb;

    private Collider2D miCollider;

    private bool lanzado;

    private bool exploto;

    private readonly HashSet<EnemigoBasico> afectados =
        new HashSet<EnemigoBasico>();

    private readonly Dictionary<VidaGusano, float> gusanos =
        new Dictionary<VidaGusano, float>();

    private readonly List<Collider2D> cercanos = new List<Collider2D>();

    #endregion

    #region Inicializacion y activacion

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        miCollider = GetComponent<Collider2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    #endregion

    #region Actualizacion durante el juego

    private void FixedUpdate()
    {
        if (!lanzado || exploto)
            return;

        Vector2 velocidad = rb.linearVelocity;
        if (velocidad.sqrMagnitude > 0.001f)
            rb.MoveRotation(Mathf.Atan2(velocidad.y, velocidad.x) * Mathf.Rad2Deg);
    }

    #endregion

    #region Deteccion de contactos

    private void OnCollisionEnter2D(Collision2D colision)
    {
        if (colision.contactCount > 0)
        {
            ContactPoint2D contacto = colision.GetContact(0);
            ComprobarImpacto(colision.collider, contacto.point, contacto.normal);
        }
        else
        {
            ComprobarImpacto(colision.collider, rb.position, Vector2.zero);
        }
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        if (!lanzado || exploto || otro == null)
            return;

        Vector2 punto = otro.ClosestPoint(rb.position);
        Vector2 normal = Vector2.zero;
        if (expandirDesdeElPiso && EsTerreno(otro))
        {
            ColliderDistance2D contacto = Physics2D.Distance(miCollider, otro);
            if (contacto.isValid && contacto.normal.y >= 0.65f)
            {
                punto = contacto.pointB;
                normal = contacto.normal;
            }
        }

        ComprobarImpacto(otro, punto, normal);
    }

    #endregion

    #region Acciones disponibles para otros componentes

    public void Lanzar(Vector2 velocidadInicial, Transform propietario)
    {
        if (lanzado || exploto)
            return;

        ExcavadoraController controlador = propietario != null
            ? propietario.GetComponentInParent<ExcavadoraController>() : null;

        if (controlador == null)
        {
            Debug.LogError("Lanzar necesita como propietario la excavadora o uno de sus hijos.", this);
            Descartar();
            return;
        }

        // Solo cobra una vez, cuando el proyectil realmente se lanza.
        if (!controlador.ConfirmarDisparoCanon())
        {
            Descartar();
            return;
        }

        lanzado = true;
        foreach (Collider2D otro in
            controlador.GetComponentsInChildren<Collider2D>())
        {
            if (otro != miCollider && otro.enabled &&
                otro.gameObject.activeInHierarchy)
                Physics2D.IgnoreCollision(miCollider, otro, true);
        }

        // La gravedad sigue modificando la trayectoria despues del lanzamiento.
        rb.linearVelocity = velocidadInicial;
        rb.rotation = Mathf.Atan2(velocidadInicial.y, velocidadInicial.x)
            * Mathf.Rad2Deg;
        Destroy(gameObject, Mathf.Max(0.1f, tiempoVida));
    }

    #endregion

    #region Logica interna

    private bool EsTerreno(Collider2D otro) =>
        (capasSuelo.value & (1 << otro.gameObject.layer)) != 0;

    private void ComprobarImpacto(Collider2D otro, Vector2 punto, Vector2 normal)
    {
        if (!lanzado || exploto || otro == null)
            return;
        ZonaDanioGusano zona = otro.GetComponent<ZonaDanioGusano>();
        if ((otro.isTrigger && zona == null) || (zona != null && !zona.EstaActiva))
            return;

        int mascara = 1 << otro.gameObject.layer;
        bool esSuelo = EsTerreno(otro);
        bool esEnemigo = (capasEnemigos.value & mascara) != 0;
        if (!esSuelo && !esEnemigo)
            return;

        // Una pared o la cara inferior de una plataforma no cuentan como piso.
        bool desdePiso = expandirDesdeElPiso && esSuelo && normal.y >= 0.65f &&
            zona == null && (!esEnemigo || otro.GetComponentInParent<EnemigoBasico>() == null);
        Explotar(punto, otro, desdePiso);
    }

    private void Explotar(Vector2 punto, Collider2D impactoDirecto, bool desdePiso)
    {
        if (exploto)
            return;

        exploto = true;
        AudioJuegoSimple.Instancia.ExplosionCanon();
        miCollider.enabled = false;
        rb.simulated = false;

        Vector2 tamano = TamanoExplosion;
        Vector2 centro = ObtenerCentroExplosion(punto, tamano, desdePiso);
        if (mostrarZonaAlExplotar)
            DibujarZonaImpacto(centro, tamano);

        BuscarAfectados(centro, tamano, impactoDirecto);

        AplicarDanioExplosion();

        FinalizarExplosion(punto);
    }

    private void FinalizarExplosion(Vector2 punto)
    {
        if (efectoExplosion != null)
        {
            GameObject efecto = Instantiate(efectoExplosion, punto, Quaternion.identity);
            Destroy(efecto, Mathf.Max(0.1f, duracionEfecto));
        }
        Destroy(gameObject);
    }

    private void AplicarDanioExplosion()
    {
        // Cada enemigo recibe un golpe aunque tenga varios colliders.
        foreach (EnemigoBasico enemigo in afectados)
        {
            if (enemigo != null)
                enemigo.RecibirDanioCanon(Mathf.Max(0, danioExplosion));
        }

        foreach (KeyValuePair<VidaGusano, float> golpe in gusanos)
        {
            if (golpe.Key != null)
                golpe.Key.RecibirDanio(golpe.Value);
        }
    }

    private void BuscarAfectados(Vector2 centro, Vector2 tamano, Collider2D impactoDirecto)
    {
        afectados.Clear();
        gusanos.Clear();
        cercanos.Clear();
        ZonaDanioGusano zonaDirecta = impactoDirecto != null
            ? impactoDirecto.GetComponent<ZonaDanioGusano>() : null;
        VidaGusano jefeDirecto = zonaDirecta != null ? zonaDirecta.Vida : null;
        if (zonaDirecta != null)
            AgregarGusano(zonaDirecta);
        EnemigoBasico directo = impactoDirecto != null
            ? impactoDirecto.GetComponentInParent<EnemigoBasico>() : null;
        if (directo != null && directo.EstaVivo)
            afectados.Add(directo);

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasEnemigos);
        filtro.useTriggers = true;
        Physics2D.OverlapBox(
            centro, tamano, 0f, filtro, cercanos);

        foreach (Collider2D cuerpo in cercanos)
        {
            ZonaDanioGusano zona = cuerpo.GetComponent<ZonaDanioGusano>();
            if (zona != null)
            {
                // Un impacto directo conserva la proteccion de la zona golpeada.
                if (zona.Vida != jefeDirecto)
                    AgregarGusano(zona);
                continue;
            }
            if (cuerpo.isTrigger)
                continue;
            EnemigoBasico enemigo = cuerpo.GetComponentInParent<EnemigoBasico>();
            if (enemigo != null && enemigo.EstaVivo)
                afectados.Add(enemigo);
        }
    }

    private void AgregarGusano(ZonaDanioGusano zona)
    {
        if (!zona.EstaActiva)
            return;
        float danio = zona.CalcularDanio(danioExplosion, TipoAtaqueGusano.Canon);
        float anterior;
        if (!gusanos.TryGetValue(zona.Vida, out anterior) || danio > anterior)
            gusanos[zona.Vida] = danio;
    }

    private Vector2 TamanoExplosion => new Vector2(
        Mathf.Max(0.01f, anchoExplosion),
        Mathf.Max(0.01f, altoExplosion));

    private Vector2 ObtenerCentroExplosion(Vector2 impacto, Vector2 tamano, bool desdePiso)
    {
        Vector2 centro = impacto + desplazamientoExplosion;
        if (desdePiso)
            centro.y = impacto.y + tamano.y * 0.5f + Mathf.Max(0f, desplazamientoExplosion.y);

        return centro;
    }

    private void Descartar()
    {
        exploto = true;
        miCollider.enabled = false;
        rb.simulated = false;
        Destroy(gameObject);
    }

    #endregion

    #region Ayudas visuales del editor

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector2 tamano = TamanoExplosion;
        Vector2 centro = ObtenerCentroExplosion(transform.position, tamano, false);
        Gizmos.DrawWireCube(new Vector3(centro.x, centro.y, transform.position.z), (Vector3)tamano);
    }

    private void DibujarZonaImpacto(Vector2 punto, Vector2 tamano)
    {
        Vector2 mitad = tamano * 0.5f;
        float z = transform.position.z;
        Vector3 abajoIzquierda = new Vector3(punto.x - mitad.x, punto.y - mitad.y, z);
        Vector3 abajoDerecha = new Vector3(punto.x + mitad.x, punto.y - mitad.y, z);
        Vector3 arribaDerecha = new Vector3(punto.x + mitad.x, punto.y + mitad.y, z);
        Vector3 arribaIzquierda = new Vector3(punto.x - mitad.x, punto.y + mitad.y, z);

        Debug.DrawLine(abajoIzquierda, abajoDerecha, Color.yellow, 1f, false);
        Debug.DrawLine(abajoDerecha, arribaDerecha, Color.yellow, 1f, false);
        Debug.DrawLine(arribaDerecha, arribaIzquierda, Color.yellow, 1f, false);
        Debug.DrawLine(arribaIzquierda, abajoIzquierda, Color.yellow, 1f, false);
    }

    #endregion

}
