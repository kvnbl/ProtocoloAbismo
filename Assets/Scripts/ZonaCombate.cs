using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class ZonaCombate : MonoBehaviour
{
    [Header("Enemigos de esta zona")]
    public EnemigoBasico[] prefabsEnemigos;

    [Min(1)] public int totalEnemigos = 12;
    [Min(1)] public int maximosVivos = 6;

    [Tooltip("Tiempo entre grupos de aparicion.")]
    [Min(0.05f)]
    public float intervaloAparicion = 1.5f;

    [Min(0f)]
    public float demoraInicial = 0.5f;

    [Header("Aparicion en grupos")]
    [Min(1)]
    public int enemigosPorGrupo = 4;

    [Tooltip("Dispersion hacia fuera de la pantalla. Usa 0 en plataformas estrechas.")]
    [Min(0f)]
    public float dispersionHorizontal = 0.5f;

    [Tooltip("0.15 varia la velocidad de caminar entre el 85% y el 115%.")]
    [Range(0f, 0.4f)]
    public float variacionVelocidad = 0.15f;

    [Header("Puntos de aparicion")]
    public Transform[] puntosAparicion;

    public bool soloFueraDePantalla = true;
    public Camera camaraJuego;

    [Tooltip("Margen extra fuera de la imagen. Para diagnosticar puntos, prueba 0.")]
    [Range(0f, 0.5f)]
    public float margenPantalla = 0.02f;

    [Header("Camara que se detiene durante el combate")]
    public ControlCamara controlCamara;

    [Header("Eventos opcionales")]
    public UnityEvent alComenzar = new UnityEvent();
    public UnityEvent alCompletar = new UnityEvent();

    [Header("Progreso (durante Play)")]
    [SerializeField] private bool iniciada;
    [SerializeField] private bool completada;
    [SerializeField] private int generados;
    [SerializeField] private int derrotados;
    [SerializeField] private int vivos;

    [SerializeField] private int puntosIzquierdaDisponibles;
    [SerializeField] private int puntosDerechaDisponibles;

    private readonly List<EnemigoBasico> enemigosActivos =
        new List<EnemigoBasico>();

    private readonly List<EnemigoBasico> prefabsValidos =
        new List<EnemigoBasico>();

    private readonly List<Transform> puntosIzquierda =
        new List<Transform>();

    private readonly List<Transform> puntosDerecha =
        new List<Transform>();

    private ExcavadoraController jugador;

    private int totalDeEstaPartida;
    private int limiteVivos;

    private float siguienteAparicion;
    private float siguienteAviso;

    private bool avisoConfiguracionMostrado;
    private bool proximoLadoIzquierda;
    private bool avisoLadoMostrado;

    public bool CombateActivo => iniciada && !completada;
    public bool Completada => completada;
    public int Derrotados => derrotados;

    public int Restantes => Mathf.Max(
        0,
        (iniciada ? totalDeEstaPartida : totalEnemigos) - derrotados
    );

    private void Reset()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnEnable()
    {
        if (CombateActivo && controlCamara != null)
            controlCamara.BloquearParaCombate(this);
    }

    private void OnDisable()
    {
        if (controlCamara != null)
            controlCamara.LiberarCombate(this);
    }

    private void OnTriggerEnter2D(Collider2D otro)
    {
        IntentarActivar(otro);
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        IntentarActivar(otro);
    }

    private void IntentarActivar(Collider2D otro)
    {
        if (!isActiveAndEnabled || iniciada || otro.isTrigger)
            return;

        ExcavadoraController encontrado =
            otro.GetComponentInParent<ExcavadoraController>();

        if (encontrado == null)
            return;

        string motivo;

        if (!PrepararConfiguracion(out motivo))
        {
            if (!avisoConfiguracionMostrado)
            {
                Debug.LogError(
                    "ZonaCombate: " + motivo,
                    this
                );

                avisoConfiguracionMostrado = true;
            }

            return;
        }

        if (!controlCamara.BloquearParaCombate(this))
            return;

        jugador = encontrado;

        totalDeEstaPartida = Mathf.Max(1, totalEnemigos);

        limiteVivos = Mathf.Clamp(
            maximosVivos,
            1,
            totalDeEstaPartida
        );

        generados = 0;
        derrotados = 0;
        vivos = 0;

        enemigosActivos.Clear();

        proximoLadoIzquierda = Random.value < 0.5f;
        avisoLadoMostrado = false;

        iniciada = true;
        completada = false;

        siguienteAparicion =
            Time.time + Mathf.Max(0f, demoraInicial);

        siguienteAviso = Time.time + 5f;

        alComenzar.Invoke();
    }

    private bool PrepararConfiguracion(out string motivo)
    {
        motivo = null;
        prefabsValidos.Clear();

        if (prefabsEnemigos != null)
        {
            foreach (EnemigoBasico prefab in prefabsEnemigos)
            {
                if (prefab != null &&
                    prefab.enabled &&
                    prefab.gameObject.activeSelf)
                {
                    prefabsValidos.Add(prefab);
                }
            }
        }

        if (prefabsValidos.Count == 0)
        {
            motivo =
                "asigna al menos un prefab de EnemigoBasico activo.";

            return false;
        }

        bool hayPunto = false;

        if (puntosAparicion != null)
        {
            foreach (Transform punto in puntosAparicion)
            {
                if (punto != null)
                    hayPunto = true;
            }
        }

        if (!hayPunto)
        {
            motivo = "asigna al menos un punto de aparicion.";
            return false;
        }

        if (camaraJuego == null)
        {
            camaraJuego = controlCamara != null
                ? controlCamara.GetComponent<Camera>()
                : Camera.main;
        }

        if (controlCamara == null && camaraJuego != null)
        {
            controlCamara =
                camaraJuego.GetComponent<ControlCamara>();
        }

        if (controlCamara == null ||
            !controlCamara.PreparadaParaCombate)
        {
            motivo =
                "asigna Control Camara y configura su Objetivo " +
                "y sus dos barreras. La camara debe ser Orthographic.";

            return false;
        }

        camaraJuego = controlCamara.CamaraJuego;

        return true;
    }

    private void Update()
    {
        if (!CombateActivo)
            return;

        if (controlCamara == null ||
            !controlCamara.BloquearParaCombate(this))
        {
            return;
        }

        ActualizarEnemigos();

        if (generados >= totalDeEstaPartida && vivos == 0)
        {
            completada = true;

            controlCamara.LiberarCombate(this);
            alCompletar.Invoke();

            return;
        }

        if (jugador == null ||
            generados >= totalDeEstaPartida ||
            vivos >= limiteVivos ||
            Time.time < siguienteAparicion)
        {
            return;
        }

        if (IntentarCrearGrupo())
        {
            siguienteAparicion =
                Time.time + Mathf.Max(0.05f, intervaloAparicion);
        }
        else
        {
            siguienteAparicion = Time.time + 0.5f;

            if (Time.time >= siguienteAviso)
            {
                Debug.LogWarning(
                    "ZonaCombate: no hay un punto disponible. " +
                    "Revisa que los puntos esten fuera de la camara " +
                    "y que los prefabs sigan asignados.",
                    this
                );

                siguienteAviso = Time.time + 5f;
            }
        }
    }

    private void ActualizarEnemigos()
    {
        for (int i = enemigosActivos.Count - 1; i >= 0; i--)
        {
            EnemigoBasico enemigo = enemigosActivos[i];

            if (enemigo == null || !enemigo.EstaVivo)
            {
                enemigosActivos.RemoveAt(i);
                derrotados++;
            }
        }

        vivos = enemigosActivos.Count;
    }

    private bool IntentarCrearGrupo()
    {
        int cantidad = Mathf.Min(
            Mathf.Max(1, enemigosPorGrupo),
            Mathf.Min(
                totalDeEstaPartida - generados,
                limiteVivos - vivos
            )
        );

        int creados = 0;

        for (int i = 0; i < cantidad; i++)
        {
            if (!IntentarCrearEnemigo())
                break;

            creados++;
        }

        return creados > 0;
    }

    private bool IntentarCrearEnemigo()
    {
        ActualizarPuntosDisponibles();

        if (prefabsValidos.Count == 0 ||
            puntosIzquierda.Count + puntosDerecha.Count == 0)
        {
            return false;
        }

        EnemigoBasico prefab =
            prefabsValidos[Random.Range(0, prefabsValidos.Count)];

        if (prefab == null ||
            !prefab.enabled ||
            !prefab.gameObject.activeSelf)
        {
            return false;
        }

        bool desdeIzquierda = proximoLadoIzquierda;

        List<Transform> candidatos = desdeIzquierda
            ? puntosIzquierda
            : puntosDerecha;

        // Si ese lado no tiene puntos validos,
        // utiliza el otro.
        if (candidatos.Count == 0)
        {
            desdeIzquierda = !desdeIzquierda;

            candidatos = desdeIzquierda
                ? puntosIzquierda
                : puntosDerecha;
        }

        Transform elegido =
            candidatos[Random.Range(0, candidatos.Count)];

        Vector3 posicion = elegido.position;

        // Desplaza un poco hacia fuera, sin cambiar la altura.
        posicion.x +=
            (desdeIzquierda ? -1f : 1f) *
            Random.Range(
                0f,
                Mathf.Max(0f, dispersionHorizontal)
            );

        EnemigoBasico nuevo = Instantiate(
            prefab,
            posicion,
            Quaternion.identity
        );

        nuevo.jugador = jugador.transform;
        nuevo.distanciaDeteccion = Mathf.Infinity;

        float variacion = Mathf.Clamp(
            variacionVelocidad,
            0f,
            0.4f
        );

        nuevo.velocidad *= Random.Range(
            1f - variacion,
            1f + variacion
        );

        proximoLadoIzquierda = !desdeIzquierda;

        enemigosActivos.Add(nuevo);

        generados++;
        vivos = enemigosActivos.Count;

        return true;
    }

    private void ActualizarPuntosDisponibles()
    {
        puntosIzquierda.Clear();
        puntosDerecha.Clear();

        int asignadosIzquierda = 0;
        int asignadosDerecha = 0;

        float centroX = camaraJuego.transform.position.x;

        if (puntosAparicion != null)
        {
            foreach (Transform punto in puntosAparicion)
            {
                if (punto == null)
                    continue;

                bool izquierda = punto.position.x < centroX;

                if (izquierda)
                    asignadosIzquierda++;
                else
                    asignadosDerecha++;

                if (!PuntoDisponible(punto.position))
                    continue;

                if (izquierda)
                    puntosIzquierda.Add(punto);
                else
                    puntosDerecha.Add(punto);
            }
        }

        puntosIzquierdaDisponibles = puntosIzquierda.Count;
        puntosDerechaDisponibles = puntosDerecha.Count;

        bool faltaLado =
            asignadosIzquierda > 0 &&
            asignadosDerecha > 0 &&
            (puntosIzquierda.Count == 0 ||
             puntosDerecha.Count == 0);

        if (faltaLado && !avisoLadoMostrado)
        {
            Debug.LogWarning(
                "ZonaCombate: hay puntos asignados a ambos lados, " +
                "pero disponibles hay " +
                puntosIzquierda.Count + " a la izquierda y " +
                puntosDerecha.Count + " a la derecha. " +
                "Aleja los puntos del borde, revisa su Z " +
                "o prueba Margen Pantalla = 0.",
                this
            );
        }

        avisoLadoMostrado = faltaLado;
    }

    private bool PuntoDisponible(Vector3 posicion)
    {
        if (!soloFueraDePantalla)
            return true;

        if (camaraJuego == null)
            return false;

        Vector3 vista =
            camaraJuego.WorldToViewportPoint(posicion);

        if (vista.z <= 0f)
            return false;

        float margen = Mathf.Max(0f, margenPantalla);

        return vista.x < -margen ||
               vista.x > 1f + margen ||
               vista.y < -margen ||
               vista.y > 1f + margen;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D zona = GetComponent<BoxCollider2D>();

        if (zona != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.DrawWireCube(
                zona.offset,
                zona.size
            );

            Gizmos.matrix = Matrix4x4.identity;
        }

        if (puntosAparicion == null)
            return;

        Gizmos.color = Color.cyan;

        foreach (Transform punto in puntosAparicion)
        {
            if (punto == null)
                continue;

            Gizmos.DrawWireSphere(punto.position, 0.25f);

            Gizmos.DrawLine(
                transform.position,
                punto.position
            );
        }
    }
}