using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class ProyectilAcidoGusano : MonoBehaviour
{
    private Rigidbody2D cuerpo;
    private CircleCollider2D zona;
    private SpriteRenderer imagen;
    private VidaJugador jugador;
    private LayerMask terreno;
    private Texture2D hoja;
    private Vector2 velocidad;
    private float gravedad;
    private float edad;
    private int danio;
    private string capaDibujo;
    private int orden;
    private bool preparado;
    private bool consumido;
    private readonly List<RaycastHit2D> impactos = new List<RaycastHit2D>(16);

    private void Awake()
    {
        cuerpo = GetComponent<Rigidbody2D>();
        zona = GetComponent<CircleCollider2D>();
        imagen = GetComponent<SpriteRenderer>();
        cuerpo.bodyType = RigidbodyType2D.Kinematic;
        cuerpo.gravityScale = 0f;
        cuerpo.linearVelocity = Vector2.zero;
        zona.isTrigger = true;
        zona.radius = 0.29f;
    }

    public void Inicializar(Texture2D textura, Vector2 impulso, float caida, int cantidad,
        VidaJugador objetivo, LayerMask capas, string sorting, int ordenDibujo)
    {
        hoja = textura;
        velocidad = impulso;
        gravedad = Mathf.Max(0f, caida);
        danio = Mathf.Max(1, cantidad);
        jugador = objetivo;
        terreno = capas;
        capaDibujo = sorting;
        orden = ordenDibujo;
        gameObject.AddComponent<VisualAcidoGusano>().Configurar(hoja, 0, 6f, true, sorting, ordenDibujo);
        preparado = true;
    }

    private void FixedUpdate()
    {
        if (!preparado || consumido)
            return;
        edad += Time.fixedDeltaTime;
        if (edad > 6f || jugador == null)
        {
            Destroy(gameObject);
            return;
        }
        // Paso balistico con aceleracion constante, consistente con el calculo del disparo.
        Vector2 desplazamiento = velocidad * Time.fixedDeltaTime +
            Vector2.down * (0.5f * gravedad * Time.fixedDeltaTime * Time.fixedDeltaTime);
        velocidad += Vector2.down * (gravedad * Time.fixedDeltaTime);
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.useTriggers = false;
        impactos.Clear();
        zona.Cast(desplazamiento.normalized, filtro, impactos, desplazamiento.magnitude);
        foreach (RaycastHit2D impacto in impactos)
            if (Valido(impacto.collider))
            {
                Vector2 punto = cuerpo.position + desplazamiento.normalized * Mathf.Max(0f, impacto.distance - 0.01f);
                Impactar(impacto.collider, punto);
                return;
            }
        cuerpo.MovePosition(cuerpo.position + desplazamiento);
    }

    private bool Valido(Collider2D otro)
    {
        return otro != null && otro != zona && !otro.isTrigger &&
            (otro.GetComponentInParent<VidaJugador>() == jugador ||
            (terreno.value & (1 << otro.gameObject.layer)) != 0);
    }

    private void OnTriggerEnter2D(Collider2D otro) => Intentar(otro);
    private void OnTriggerStay2D(Collider2D otro) => Intentar(otro);

    private void Intentar(Collider2D otro)
    {
        if (preparado && !consumido && Valido(otro))
            Impactar(otro, cuerpo.position);
    }

    private void Impactar(Collider2D otro, Vector2 punto)
    {
        if (consumido)
            return;
        consumido = true;
        zona.enabled = false;
        imagen.enabled = false;
        VisualAcidoGusano.CrearEfecto(hoja, 1,
            new Vector3(punto.x, punto.y, transform.position.z),
            transform.localScale.x * 1.5f, 0.32f, false, capaDibujo, orden + 1);
        if (jugador != null && otro.GetComponentInParent<VidaJugador>() == jugador)
            jugador.RecibirDanio(danio);
        Destroy(gameObject);
    }
}
