using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EscenaFinalJuego : MonoBehaviour
{
    public VidaGusano jefe;
    public ExcavadoraController jugador;
    public ControlCamara controlCamara;
    public ParedFinalJuego pared;
    public Transform finRecorrido;
    public GameObject[] zonasMuerteDuranteFinal;
    [Min(0f)] public float pausaTrasJefe = 0.8f;
    [Min(0.2f)] public float tiempoRomperPared = 1.2f;
    [Min(1f)] public float avanzarDespues = 10f;
    [Min(1f)] public float tiempoMaximoPorTramo = 15f;
    [Tooltip("Fraccion del ultimo recorrido antes de empezar a oscurecer. 0 = desde el primer paso.")]
    [Range(0f, 0.9f)] public float inicioFundidoSalida = 0f;
    public string tituloFinal = "MISIÓN COMPLETADA";
    public bool permitirReinicio = true;

    private Rigidbody2D cuerpo;
    private VidaJugador vida;
    private bool iniciada;
    private bool enCurso;
    private bool terminado;
    private bool falloMovimiento;
    private bool camaraAntes;
    private bool proteccionAntes;
    private float escalaTiempoAntes;
    private PantallaTransicionJuego pantalla;
    private AudioJuegoSimple.EstadoMusica musicaAntes;
    private readonly Dictionary<GameObject, bool> zonasAntes = new Dictionary<GameObject, bool>();

    private void OnEnable()
    {
        if (jefe == null)
            jefe = FindFirstObjectByType<VidaGusano>();
        if (jefe != null)
            jefe.Murio += Comenzar;
    }

    private void Comenzar()
    {
        if (!isActiveAndEnabled || iniciada)
            return;
        if (jugador == null)
            jugador = FindFirstObjectByType<ExcavadoraController>();
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        if (jugador == null || controlCamara == null || pared == null ||
            pared.colliderPared == null || pared.colliderPared.isTrigger ||
            !pared.colliderPared.transform.IsChildOf(pared.Grupo.transform) ||
            transform.IsChildOf(pared.Grupo.transform) ||
            jugador.transform.IsChildOf(pared.Grupo.transform) ||
            controlCamara.transform.IsChildOf(pared.Grupo.transform))
        {
            Debug.LogError("EscenaFinalJuego: asigna jugador, camara y una ParedFinalJuego " +
                "con collider SOLIDO dentro de su propio Grupo Pared. No uses el Tilemap del suelo.", this);
            return;
        }
        cuerpo = jugador.GetComponent<Rigidbody2D>();
        vida = jugador.GetComponent<VidaJugador>();
        if (cuerpo == null || vida == null || jugador.EnSecuencia)
        {
            Debug.LogError("No se pudo iniciar el final: revisa Rigidbody2D, VidaJugador y que no haya otra secuencia.", this);
            return;
        }
        if (pared.Limites.max.x <= cuerpo.position.x ||
            (finRecorrido != null && finRecorrido.position.x <= pared.Limites.max.x + 0.5f))
        {
            Debug.LogError("La pared debe estar a la derecha del jugador y Fin Recorrido debe quedar despues de la pared.", this);
            return;
        }
        if (zonasMuerteDuranteFinal != null)
            foreach (GameObject zona in zonasMuerteDuranteFinal)
                if (zona != null && (jugador.transform.IsChildOf(zona.transform) ||
                    controlCamara.transform.IsChildOf(zona.transform) || transform.IsChildOf(zona.transform)))
                {
                    Debug.LogError("En Zonas Muerte Durante Final arrastra solo los objetos de muerte, no padres de la escena.", this);
                    return;
                }
        AudioJuegoSimple audio = AudioJuegoSimple.Instancia;
        musicaAntes = audio.EstadoActual == AudioJuegoSimple.EstadoMusica.Jefe
            ? AudioJuegoSimple.EstadoMusica.SoloAmbiente : audio.EstadoActual;
        audio.IniciarFinal();
        iniciada = true;
        enCurso = true;
        proteccionAntes = vida.ProtegidoPorSecuencia;
        camaraAntes = controlCamara.enabled;
        escalaTiempoAntes = Time.timeScale;
        vida.ProtegidoPorSecuencia = true;
        jugador.EstablecerControlSecuencia(true);
        controlCamara.enabled = false; // Desactiva las barreras; la Camera sigue renderizando.
        if (zonasMuerteDuranteFinal != null)
            foreach (GameObject zona in zonasMuerteDuranteFinal)
                if (zona != null && !zonasAntes.ContainsKey(zona))
                {
                    zonasAntes.Add(zona, zona.activeSelf);
                    zona.SetActive(false);
                }
        StartCoroutine(Final());
    }

    private IEnumerator Final()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, pausaTrasJefe));
        Vector3 escala = jugador.transform.localScale;
        escala.x = Mathf.Abs(escala.x);
        jugador.transform.localScale = escala;
        Physics2D.SyncTransforms();
        float frente = 0.5f;
        foreach (Collider2D collider in jugador.GetComponentsInChildren<Collider2D>())
            if (collider.enabled && !collider.isTrigger)
                frente = Mathf.Max(frente, collider.bounds.max.x - cuerpo.position.x);
        float puntoRomper = pared.Limites.min.x - frente - 0.12f;
        yield return Avanzar(puntoRomper, false);
        if (falloMovimiento)
        {
            Restaurar();
            yield break;
        }
        jugador.EstablecerControlSecuencia(true, 0f, true);
        cuerpo.linearVelocity = new Vector2(0f, cuerpo.linearVelocity.y);
        yield return new WaitForSeconds(Mathf.Max(0.2f, tiempoRomperPared));
        pared.Romper();
        yield return new WaitForSeconds(0.15f);
        float destino = finRecorrido != null ? finRecorrido.position.x : cuerpo.position.x + avanzarDespues;
        if (destino <= cuerpo.position.x)
        {
            Debug.LogError("Fin Recorrido debe estar a la derecha de la pared final.", this);
            Restaurar();
            yield break;
        }
        pantalla = PantallaTransicionJuego.Instancia;
        pantalla.FijarNegro(0f);
        yield return Avanzar(destino, true, true);
        if (falloMovimiento)
        {
            Restaurar();
            yield break;
        }
        jugador.EstablecerControlSecuencia(true, 0f, false);
        cuerpo.linearVelocity = Vector2.zero;
        AudioJuegoSimple.DetenerTaladroSiExiste();
        pantalla.FijarNegro(1f);
        pantalla.MostrarFinal(tituloFinal, permitirReinicio ? "R: volver a jugar" : "Gracias por jugar");
        AudioJuegoSimple.Instancia.Victoria();
        terminado = true;
        Time.timeScale = 0f;
    }

    private IEnumerator Avanzar(float destino, bool taladro, bool oscurecer = false)
    {
        falloMovimiento = false;
        float inicio = cuerpo.position.x;
        float longitud = Mathf.Max(0.01f, destino - inicio);
        float avanceMaximo = 0f;
        jugador.EstablecerControlSecuencia(true, 1f, taladro);
        float tiempo = 0f;
        while (cuerpo != null && cuerpo.position.x < destino - 0.08f)
        {
            tiempo += Time.deltaTime;
            if (tiempo > tiempoMaximoPorTramo)
            {
                falloMovimiento = true;
                Debug.LogError("Final detenido por un obstaculo: deja suelo continuo y camino libre " +
                    "hasta la pared y despues de ella. Se devuelve el control al jugador.", this);
                break;
            }
            if (oscurecer && pantalla != null)
            {
                avanceMaximo = Mathf.Max(avanceMaximo,
                    Mathf.Clamp01((cuerpo.position.x - inicio) / longitud));
                float progreso = Mathf.InverseLerp(Mathf.Clamp(inicioFundidoSalida, 0f, 0.9f), 1f, avanceMaximo);
                pantalla.FijarNegro(Mathf.SmoothStep(0f, 1f, progreso));
            }
            Vector3 posicion = controlCamara.transform.position;
            posicion.x = Mathf.Lerp(posicion.x, cuerpo.position.x + controlCamara.offsetX, Time.deltaTime * 4f);
            controlCamara.transform.position = posicion;
            yield return null;
        }
        if (oscurecer && !falloMovimiento && pantalla != null)
            pantalla.FijarNegro(1f);
        jugador.EstablecerControlSecuencia(true, 0f, taladro);
        if (cuerpo != null)
            cuerpo.linearVelocity = new Vector2(0f, cuerpo.linearVelocity.y);
    }

    private void Update()
    {
        if (terminado && permitirReinicio && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = escalaTiempoAntes;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    private void Restaurar()
    {
        if (!enCurso)
            return;
        Time.timeScale = escalaTiempoAntes;
        if (AudioJuegoSimple.Existente != null)
            AudioJuegoSimple.Existente.EstablecerMusica(musicaAntes, true);
        if (jugador != null)
            jugador.EstablecerControlSecuencia(false);
        if (vida != null)
            vida.ProtegidoPorSecuencia = proteccionAntes;
        if (controlCamara != null)
        {
            if (jugador != null)
                controlCamara.ReiniciarSeguimientoEn(jugador.transform.position);
            controlCamara.enabled = camaraAntes;
        }
        foreach (KeyValuePair<GameObject, bool> zona in zonasAntes)
            if (zona.Key != null)
                zona.Key.SetActive(zona.Value);
        zonasAntes.Clear();
        AudioJuegoSimple.DetenerTaladroSiExiste();
        if (pantalla != null)
            pantalla.OcultarFinal();
        enCurso = false;
        terminado = false;
    }

    private void OnDisable()
    {
        if (jefe != null)
            jefe.Murio -= Comenzar;
        StopAllCoroutines();
        Restaurar();
    }
}
