using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class BajadaSalaJefe : MonoBehaviour
{
    public Transform destinoSala;
    public ControlCamara controlCamara;
    public ZonaJefeGusano zonaJefe;
    // Se conserva oculta por compatibilidad con escenas del paquete anterior.
    [HideInInspector] public GameObject[] zonasMuerteSuperiores;
    [Min(0.1f)] public float duracionFundido = 0.45f;
    private bool usada;
    private bool enCurso;
    private bool completada;
    private ExcavadoraController jugador;
    private Rigidbody2D cuerpo;
    private VidaJugador vida;
    private bool proteccionAntes;
    private bool camaraAntes;
    private bool zonaAntes;
    private bool simulacionAntes;
    private Vector3 posicionAntes;
    private PantallaTransicionJuego pantalla;
    private bool avisoDestino;
    private AudioJuegoSimple.EstadoMusica musicaAntes;
    private bool musicaCambiada;

    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void OnTriggerEnter2D(Collider2D otro) => Intentar(otro);
    private void OnTriggerStay2D(Collider2D otro) => Intentar(otro);

    private void Intentar(Collider2D otro)
    {
        if (!isActiveAndEnabled || usada || otro.isTrigger)
            return;
        ExcavadoraController encontrado = otro.GetComponentInParent<ExcavadoraController>();
        if (encontrado == null || encontrado.EnSecuencia)
            return;
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        if (controlCamara != null && controlCamara.EnCombate)
            return;
        if (destinoSala == null || controlCamara == null)
        {
            Debug.LogError("BajadaSalaJefe: asigna Destino Sala y Control Camara.", this);
            return;
        }
        jugador = encontrado;
        cuerpo = jugador.GetComponent<Rigidbody2D>();
        vida = jugador.GetComponent<VidaJugador>();
        if (cuerpo == null || vida == null)
            return;
        if (!DestinoLibre())
        {
            if (!avisoDestino)
                Debug.LogError("Destino Sala solapa el terreno. Colocalo un poco sobre el suelo, " +
                    "dejando espacio para toda la excavadora y el taladro.", this);
            avisoDestino = true;
            return;
        }
        musicaAntes = AudioJuegoSimple.Instancia.EstadoActual;
        usada = true;
        enCurso = true;
        posicionAntes = jugador.transform.position;
        proteccionAntes = vida.ProtegidoPorSecuencia;
        camaraAntes = controlCamara.enabled;
        simulacionAntes = cuerpo.simulated;
        zonaAntes = zonaJefe != null && zonaJefe.enabled;
        vida.ProtegidoPorSecuencia = true;
        jugador.EstablecerControlSecuencia(true);
        controlCamara.enabled = false;
        if (zonaJefe != null)
            zonaJefe.enabled = false;
        pantalla = PantallaTransicionJuego.Instancia;
        StartCoroutine(Bajar());
    }

    private bool DestinoLibre()
    {
        if (jugador.groundLayer.value == 0)
            return true;
        Bounds limites = new Bounds(jugador.transform.position, Vector3.zero);
        bool encontrado = false;
        foreach (Collider2D collider in jugador.GetComponentsInChildren<Collider2D>())
            if (collider.enabled && !collider.isTrigger)
            {
                if (!encontrado)
                {
                    limites = collider.bounds;
                    encontrado = true;
                }
                else limites.Encapsulate(collider.bounds);
            }
        if (!encontrado)
            return true;
        Vector2 centro = destinoSala.position + (limites.center - jugador.transform.position);
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(jugador.groundLayer);
        filtro.useTriggers = false;
        List<Collider2D> resultados = new List<Collider2D>();
        Physics2D.OverlapBox(centro, (Vector2)limites.size * 0.95f, 0f, filtro, resultados);
        foreach (Collider2D collider in resultados)
            if (!collider.transform.IsChildOf(jugador.transform))
                return false;
        return true;
    }

    private IEnumerator Bajar()
    {
        yield return pantalla.Fundir(1f, duracionFundido);
        cuerpo.simulated = false;
        cuerpo.linearVelocity = Vector2.zero;
        jugador.transform.position = destinoSala.position;
        cuerpo.position = destinoSala.position;
        Physics2D.SyncTransforms();
        cuerpo.angularVelocity = 0f;
        controlCamara.ReiniciarSeguimientoEn(destinoSala.position);
        AudioJuegoSimple.Instancia.SoloAmbiente();
        musicaCambiada = true;
        cuerpo.simulated = simulacionAntes;
        controlCamara.enabled = camaraAntes;
        yield return null; // Deja actualizar encuadre y barreras antes de revelar.
        yield return pantalla.Fundir(0f, duracionFundido);
        jugador.EstablecerControlSecuencia(false);
        vida.ProtegidoPorSecuencia = proteccionAntes;
        if (zonaJefe != null)
            zonaJefe.enabled = zonaAntes;
        completada = true;
        enCurso = false;
    }

    private void OnDisable()
    {
        if (!enCurso || completada)
            return;
        StopAllCoroutines();
        if (musicaCambiada && AudioJuegoSimple.Existente != null)
            AudioJuegoSimple.Existente.EstablecerMusica(musicaAntes, true);
        musicaCambiada = false;
        if (jugador != null)
        {
            jugador.transform.position = posicionAntes;
            jugador.EstablecerControlSecuencia(false);
        }
        if (cuerpo != null)
        {
            cuerpo.position = posicionAntes;
            cuerpo.linearVelocity = Vector2.zero;
            cuerpo.simulated = simulacionAntes;
        }
        if (vida != null)
            vida.ProtegidoPorSecuencia = proteccionAntes;
        if (controlCamara != null)
        {
            controlCamara.ReiniciarSeguimientoEn(posicionAntes);
            controlCamara.enabled = camaraAntes;
        }
        if (zonaJefe != null)
            zonaJefe.enabled = zonaAntes;
        if (pantalla != null)
            pantalla.FijarNegro(0f);
        enCurso = false;
        usada = false;
    }
}
