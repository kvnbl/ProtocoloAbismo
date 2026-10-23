using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class ProyectilCanon : MonoBehaviour
{
    [Header("Impacto")]
    public LayerMask capasSuelo;
    public LayerMask capasEnemigos;

    [Header("Explosion")]
    [Min(0)] public int danioExplosion = 3;
    [Min(0f)] public float radioExplosion = 1.5f;
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        miCollider = GetComponent<Collider2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

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

    private void FixedUpdate()
    {
        if (!lanzado || exploto)
            return;

        Vector2 velocidad = rb.linearVelocity;
        if (velocidad.sqrMagnitude > 0.001f)
            rb.MoveRotation(Mathf.Atan2(velocidad.y, velocidad.x) * Mathf.Rad2Deg);
    }

    private void OnCollisionEnter2D(Collision2D colision)
    {
        Vector2 punto = colision.contactCount > 0
            ? colision.GetContact(0).point : (Vector2)transform.position;
        ComprobarImpacto(colision.collider, punto);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        ComprobarImpacto(otro, otro.ClosestPoint(rb.position));
    }

    private void ComprobarImpacto(Collider2D otro, Vector2 punto)
    {
        if (!lanzado || exploto || otro == null)
            return;
        ZonaDanioGusano zona = otro.GetComponent<ZonaDanioGusano>();
        if ((otro.isTrigger && zona == null) || (zona != null && !zona.EstaActiva))
            return;

        int mascara = 1 << otro.gameObject.layer;
        bool esSuelo = (capasSuelo.value & mascara) != 0;
        bool esEnemigo = (capasEnemigos.value & mascara) != 0;
        if (!esSuelo && !esEnemigo)
            return;

        Explotar(punto, otro);
    }

    private void Explotar(Vector2 punto, Collider2D impactoDirecto)
    {
        if (exploto)
            return;

        exploto = true;
        AudioJuegoSimple.Instancia.ExplosionCanon();
        miCollider.enabled = false;
        rb.simulated = false;

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
        Physics2D.OverlapCircle(
            punto, Mathf.Max(0f, radioExplosion), filtro, cercanos);

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

        if (efectoExplosion != null)
        {
            GameObject efecto = Instantiate(efectoExplosion, punto, Quaternion.identity);
            Destroy(efecto, Mathf.Max(0.1f, duracionEfecto));
        }
        Destroy(gameObject);
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

    private void Descartar()
    {
        exploto = true;
        miCollider.enabled = false;
        rb.simulated = false;
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radioExplosion));
    }
}
