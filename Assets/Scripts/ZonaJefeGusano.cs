using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class ZonaJefeGusano : MonoBehaviour
{
    [Header("Referencias")]
    public ControlCamara controlCamara;
    public GusanoModularPrueba gusano;
    [Tooltip("Vacio opcional que marca el centro del encuadre del jefe.")]
    public Transform centroSala;

    [Header("Entrada a la sala")]
    [Min(1f)] public float factorAlejamiento = 1.3f;
    [Min(0f)] public float duracionTransicion = 1.2f;
    [Min(0f)] public float demoraJefe = 0.25f;

    [Header("Estado durante Play")]
    [SerializeField] private bool iniciada;
    [SerializeField] private bool completada;

    private VidaGusano vida;
    private bool avisoMostrado;
    private float tamanoEncuadre;
    private AudioJuegoSimple.EstadoMusica musicaAntes;
    private bool cambioMusica;

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (gusano != null)
            gusano.iniciarAutomaticamente = false;
    }

    private void Start()
    {
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        if (gusano != null && !iniciada)
        {
            gusano.iniciarAutomaticamente = false;
            gusano.DetenerPrueba();
        }
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        IntentarEntrar(otro);
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        IntentarEntrar(otro);
    }

    private void IntentarEntrar(Collider2D otro)
    {
        if (!isActiveAndEnabled || iniciada || completada || otro.isTrigger ||
            otro.GetComponentInParent<ExcavadoraController>() == null)
            return;

        if (controlCamara == null || !controlCamara.PreparadaParaCombate)
        {
            Avisar("Asigna Control Camara con Objetivo y sus dos barreras configurados.");
            return;
        }
        if (gusano == null || !gusano.isActiveAndEnabled)
        {
            Avisar("Asigna el GusanoPrueba de la escena y dejalo activo.");
            return;
        }
        if (!gusano.Preparado)
            return;

        vida = gusano.GetComponent<VidaGusano>();
        if (vida == null || !vida.EstaVivo)
        {
            Avisar("El gusano debe tener VidaGusano y estar vivo.");
            return;
        }

        Camera camara = controlCamara.CamaraJuego;
        Vector3 centro = centroSala != null
            ? centroSala.position : camara.transform.position;
        tamanoEncuadre = camara.orthographicSize * Mathf.Max(1f, factorAlejamiento);
        if (!controlCamara.BloquearParaJefe(this, centro,
            tamanoEncuadre, duracionTransicion))
            return; // Otra zona tiene la camara: espera dentro del trigger.

        musicaAntes = AudioJuegoSimple.Instancia.EstadoActual;
        AudioJuegoSimple.Instancia.MusicaDelJefe();
        cambioMusica = true;
        iniciada = true;
        avisoMostrado = false;
        vida.Murio += CompletarSala;
        gusano.DetenerPrueba();
        StartCoroutine(EmpezarTrasEncuadre());
    }

    private IEnumerator EmpezarTrasEncuadre()
    {
        while (controlCamara != null && controlCamara.isActiveAndEnabled &&
            controlCamara.EsPropietarioBloqueo(this) &&
            controlCamara.EncuadreEnTransicion)
            yield return null;

        if (demoraJefe > 0f)
            yield return new WaitForSeconds(demoraJefe);

        if (controlCamara == null || !controlCamara.isActiveAndEnabled ||
            !controlCamara.EsPropietarioBloqueo(this) ||
            gusano == null || !gusano.isActiveAndEnabled ||
            vida == null || !vida.EstaVivo)
        {
            CancelarSala();
            yield break;
        }
        gusano.IniciarPasada();
    }

    private void CompletarSala()
    {
        if (completada)
            return;
        completada = true;
        if (AudioJuegoSimple.Existente != null &&
            AudioJuegoSimple.Existente.EstadoActual == AudioJuegoSimple.EstadoMusica.Jefe)
            AudioJuegoSimple.Existente.SoloAmbiente();
        cambioMusica = false;
        StopAllCoroutines();
        if (vida != null)
            vida.Murio -= CompletarSala;
        if (controlCamara != null)
            controlCamara.LiberarCombate(this);
    }

    private void CancelarSala()
    {
        StopAllCoroutines();
        if (cambioMusica && AudioJuegoSimple.Existente != null &&
            AudioJuegoSimple.Existente.EstadoActual == AudioJuegoSimple.EstadoMusica.Jefe)
            AudioJuegoSimple.Existente.EstablecerMusica(musicaAntes);
        cambioMusica = false;
        if (vida != null)
            vida.Murio -= CompletarSala;
        if (iniciada && !completada)
        {
            if (gusano != null)
                gusano.DetenerPrueba();
            iniciada = false;
        }
        if (controlCamara != null)
            controlCamara.LiberarCombate(this);
    }

    private void OnDisable()
    {
        CancelarSala();
    }

    private void Avisar(string mensaje)
    {
        if (!avisoMostrado)
            Debug.LogError("ZonaJefeGusano: " + mensaje, this);
        avisoMostrado = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (controlCamara == null)
            return;
        Camera camara = controlCamara.GetComponent<Camera>();
        if (camara == null || !camara.orthographic)
            return;
        Vector3 centro = centroSala != null ? centroSala.position : camara.transform.position;
        centro.z = transform.position.z;
        float tamano = Application.isPlaying && iniciada
            ? tamanoEncuadre : camara.orthographicSize * Mathf.Max(1f, factorAlejamiento);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(centro, new Vector3(tamano * 2f * camara.aspect, tamano * 2f, 0f));
    }
}
