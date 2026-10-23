using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ControlCamara : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform objetivo;

    [Header("Configuracion")]
    public float offsetX = 3f;
    public float offsetY = 3f;
    [Min(0f)] public float suavizado = 5f;

    [Header("Barreras de los bordes de la camara")]
    public BoxCollider2D barreraIzquierda;
    public BoxCollider2D barreraDerecha;
    [Min(0.1f)] public float grosorBarreras = 0.75f;
    [Min(0f)] public float margenInterior = 0.1f;
    [Min(0f)] public float extensionVertical = 5f;

    [Header("Muerte al caer fuera del encuadre")]
    public bool morirPorDebajo = true;
    public bool morirPorLaterales = false;
    public bool morirPorArriba = false;
    [Tooltip("Fraccion de la pantalla que debe superar el cuerpo COMPLETO.")]
    [Min(0f)] public float margenMuerte = 0.03f;
    [Min(0f)] public float demoraMuerteFuera = 0.2f;
    [Min(0f)] public float graciaTrasTraslado = 0.5f;
    private float tiempoFuera;
    private float noComprobarHasta;
    private Transform raizMuerte;
    private VidaJugador vidaObjetivo;
    private ExcavadoraController excavadoraObjetivo;
    private Collider2D[] collidersObjetivo;

    private Camera camara;
    private float maxX;
    private float alturaInicialJugador;
    private bool inicializada;
    private Object propietarioBloqueo;
    private Vector3 posicionBloqueada;
    private bool zoomJefe;
    private bool transicion;
    private bool restaurando;
    private float tamanoNormal;
    private float tiempoZoom;
    private float duracionZoom;
    private float duracionRetorno;
    private float tamanoOrigen;
    private float tamanoDestino;
    private Vector3 posicionOrigen;
    private Vector3 posicionDestino;
    private float temblorRestante;
    private float temblorDuracion;
    private float temblorFuerza;
    private bool proyeccionTemblor;

    public Camera CamaraJuego => camara;
    public bool EnCombate => propietarioBloqueo != null;
    public bool EncuadreEnTransicion => transicion;

    public bool EsPropietarioBloqueo(Object solicitante) =>
        solicitante != null && propietarioBloqueo == solicitante;
    public bool PreparadaParaCombate =>
        isActiveAndEnabled && camara != null && camara.orthographic &&
        objetivo != null && BarrerasValidas();

    private void Awake()
    {
        camara = GetComponent<Camera>();
    }

    private void Start()
    {
        Inicializar();
        if (!PreparadaParaCombate)
        {
            Debug.LogError(
                "ControlCamara: usa una camara Orthographic y asigna Objetivo " +
                "y dos barreras hijas de la camara, separadas, activas " +
                "y sin Rigidbody2D.", this);
        }
        ActualizarBarreras();
    }

    private void Inicializar()
    {
        if (inicializada || objetivo == null)
            return;

        maxX = objetivo.position.x + offsetX;
        alturaInicialJugador = objetivo.position.y;
        transform.position = new Vector3(
            maxX, alturaInicialJugador + offsetY, transform.position.z);
        inicializada = true;
    }

    private void LateUpdate()
    {
        Inicializar();
        if (zoomJefe && propietarioBloqueo == null)
            IniciarRetornoJefe();

        if (transicion)
        {
            AvanzarTransicion();
        }
        else if (EnCombate)
        {
            // No acumula un destino de seguimiento mientras esta fija.
            transform.position = posicionBloqueada;
        }
        else if (objetivo != null)
        {
            maxX = Mathf.Max(maxX, objetivo.position.x + offsetX);
            Vector3 destino = new Vector3(
                maxX, alturaInicialJugador + offsetY, transform.position.z);
            Vector3 posicion = Vector3.Lerp(
                transform.position, destino, Mathf.Max(0f, suavizado) * Time.deltaTime);
            // Tampoco retrocede si acaba de terminar un combate.
            posicion.x = Mathf.Max(transform.position.x, posicion.x);
            transform.position = posicion;
        }

        ActualizarBarreras();
        ActualizarTemblor();
        ComprobarMuerteFuera();
    }

    public bool BloquearParaCombate(Object solicitante)
    {
        Inicializar();
        if (solicitante == null || !PreparadaParaCombate || restaurando)
            return false;
        if (EnCombate)
            return propietarioBloqueo == solicitante;

        propietarioBloqueo = solicitante;
        posicionBloqueada = transform.position;
        maxX = posicionBloqueada.x;
        ActualizarBarreras();
        return true;
    }

    public void LiberarCombate(Object solicitante)
    {
        if (solicitante == null || propietarioBloqueo != solicitante)
            return;

        propietarioBloqueo = null;
        if (zoomJefe)
        {
            IniciarRetornoJefe();
            return;
        }
        maxX = transform.position.x;
        // Retoma el seguimiento suavemente desde la posicion actual.
    }


    public bool BloquearParaJefe(Object solicitante, Vector3 centro,
        float tamano, float segundos)
    {
        Inicializar();
        if (solicitante == null || !PreparadaParaCombate || restaurando)
            return false;
        if (EnCombate)
            return propietarioBloqueo == solicitante;

        propietarioBloqueo = solicitante;
        tamanoNormal = camara.orthographicSize;
        duracionRetorno = Mathf.Max(0f, segundos);
        zoomJefe = true;
        posicionBloqueada = transform.position;
        maxX = transform.position.x;
        centro.z = transform.position.z;
        EmpezarTransicion(centro, Mathf.Max(tamanoNormal, tamano), segundos);
        ActualizarBarreras();
        return true;
    }

    private void EmpezarTransicion(Vector3 destino, float tamano, float segundos)
    {
        AjustarParaContenerJugador(ref destino, ref tamano);
        posicionOrigen = transform.position;
        tamanoOrigen = camara.orthographicSize;
        posicionDestino = destino;
        tamanoDestino = tamano;
        tiempoZoom = 0f;
        duracionZoom = Mathf.Max(0f, segundos);
        transicion = true;
        if (duracionZoom <= 0f)
            AvanzarTransicion();
    }

    private void AvanzarTransicion()
    {
        tiempoZoom += Time.deltaTime;
        float t = duracionZoom <= 0f ? 1f : Mathf.Clamp01(tiempoZoom / duracionZoom);
        float suave = t * t * (3f - 2f * t);
        Vector3 posicion = Vector3.Lerp(posicionOrigen, posicionDestino, suave);
        float tamano = Mathf.Lerp(tamanoOrigen, tamanoDestino, suave);
        AjustarParaContenerJugador(ref posicion, ref tamano);
        transform.position = posicion;
        camara.orthographicSize = tamano;
        posicionBloqueada = posicion;

        if (t < 1f)
            return;

        transicion = false;
        maxX = transform.position.x;
        if (restaurando)
        {
            restaurando = false;
            // Retoma la altura de esta sala y evita volver al nivel anterior.
            alturaInicialJugador = transform.position.y - offsetY;
        }
    }

    private void IniciarRetornoJefe()
    {
        zoomJefe = false;
        propietarioBloqueo = null;
        restaurando = true;
        Vector3 destino = transform.position;
        if (objetivo != null)
            destino.x = objetivo.position.x + offsetX;
        EmpezarTransicion(destino, tamanoNormal, duracionRetorno);
    }

    private void AjustarParaContenerJugador(ref Vector3 posicion, ref float tamano)
    {
        tamano = Mathf.Max(0.01f, tamano);
        if (objetivo == null)
            return;

        Transform raiz = objetivo;
        Rigidbody2D cuerpo = objetivo.GetComponentInParent<Rigidbody2D>();
        if (cuerpo != null)
            raiz = cuerpo.transform;

        Bounds limites = new Bounds(objetivo.position, Vector3.zero);
        bool encontrado = false;
        foreach (Collider2D collider in raiz.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger ||
                !collider.gameObject.activeInHierarchy ||
                collider.transform.IsChildOf(transform))
                continue;
            if (!encontrado)
            {
                limites = collider.bounds;
                encontrado = true;
            }
            else
            {
                limites.Encapsulate(collider.bounds);
            }
        }

        float margen = Mathf.Max(0f, margenInterior) + 0.05f;
        float aspecto = Mathf.Max(0.01f, camara.aspect);
        tamano = Mathf.Max(tamano, limites.extents.y + margen + 0.001f);
        tamano = Mathf.Max(tamano, (limites.extents.x + margen + 0.001f) / aspecto);
        float mitadAncho = tamano * aspecto;
        posicion.x = Mathf.Clamp(posicion.x,
            limites.max.x + margen - mitadAncho,
            limites.min.x - margen + mitadAncho);
        posicion.y = Mathf.Clamp(posicion.y,
            limites.max.y + margen - tamano,
            limites.min.y - margen + tamano);
    }

    public void Sacudir(float segundos, float intensidad)
    {
        if (segundos <= 0f || intensidad <= 0f)
            return;
        AudioJuegoSimple.Instancia.Temblor();
        temblorRestante = Mathf.Max(temblorRestante, segundos);
        temblorDuracion = temblorRestante;
        temblorFuerza = Mathf.Max(temblorFuerza, intensidad);
    }

    // Teletransporte de una sala a otra bajo el fundido negro.
    public void ReiniciarSeguimientoEn(Vector3 posicionJugador)
    {
        tiempoFuera = 0f;
        noComprobarHasta = Time.time + Mathf.Max(0f, graciaTrasTraslado);
        propietarioBloqueo = null;
        zoomJefe = false;
        transicion = false;
        restaurando = false;
        temblorRestante = 0f;
        temblorFuerza = 0f;
        if (camara != null && proyeccionTemblor)
            camara.ResetProjectionMatrix();
        proyeccionTemblor = false;
        alturaInicialJugador = posicionJugador.y;
        maxX = posicionJugador.x + offsetX;
        transform.position = new Vector3(maxX, posicionJugador.y + offsetY, transform.position.z);
        posicionBloqueada = transform.position;
        inicializada = true;
        if (isActiveAndEnabled)
            ActualizarBarreras();
    }

    // Se consulta al jugador: no depende de un objeto "Zona Muerte".
    private void ComprobarMuerteFuera()
    {
        if (camara == null || objetivo == null || !camara.orthographic ||
            Time.timeScale <= 0f || (!morirPorDebajo && !morirPorLaterales && !morirPorArriba))
        {
            tiempoFuera = 0f;
            return;
        }
        VidaJugador actual = objetivo.GetComponentInParent<VidaJugador>();
        if (actual == null)
        {
            tiempoFuera = 0f;
            return;
        }
        if (vidaObjetivo != actual || raizMuerte != actual.transform)
        {
            vidaObjetivo = actual;
            raizMuerte = actual.transform;
            excavadoraObjetivo = raizMuerte.GetComponent<ExcavadoraController>();
            collidersObjetivo = raizMuerte.GetComponentsInChildren<Collider2D>();
        }
        if (vidaObjetivo.ProtegidoPorSecuencia ||
            (excavadoraObjetivo != null && excavadoraObjetivo.EnSecuencia))
        {
            tiempoFuera = 0f;
            noComprobarHasta = Time.time + Mathf.Max(0f, graciaTrasTraslado);
            return;
        }
        if (Time.time < noComprobarHasta)
        {
            tiempoFuera = 0f;
            return;
        }
        Bounds cuerpo = new Bounds(raizMuerte.position, Vector3.zero);
        bool encontrado = false;
        foreach (Collider2D col in collidersObjetivo)
        {
            if (col == null || !col.enabled || col.isTrigger || !col.gameObject.activeInHierarchy)
                continue;
            if (!encontrado) { cuerpo = col.bounds; encontrado = true; }
            else cuerpo.Encapsulate(col.bounds);
        }
        // Se usa la matriz de proyeccion SIN temblor: sacudir no debe matar.
        Matrix4x4 proyeccion = camara.projectionMatrix;
        if (proyeccionTemblor)
            camara.ResetProjectionMatrix();
        Vector3 minimo = camara.WorldToViewportPoint(cuerpo.min);
        Vector3 maximo = camara.WorldToViewportPoint(cuerpo.max);
        if (proyeccionTemblor)
            camara.projectionMatrix = proyeccion;
        float margen = Mathf.Max(0f, margenMuerte);
        bool fuera = (morirPorDebajo && maximo.y < -margen) ||
            (morirPorArriba && minimo.y > 1f + margen) ||
            (morirPorLaterales && (maximo.x < -margen || minimo.x > 1f + margen));
        tiempoFuera = fuera ? tiempoFuera + Time.deltaTime : 0f;
        if (fuera && tiempoFuera >= Mathf.Max(0f, demoraMuerteFuera))
            vidaObjetivo.MorirFueraDeCamara();
    }

    private void ActualizarTemblor()
    {
        if (camara == null)
            return;
        if (proyeccionTemblor)
        {
            camara.ResetProjectionMatrix();
            proyeccionTemblor = false;
        }
        if (temblorRestante <= 0f || !camara.orthographic)
        {
            temblorFuerza = 0f;
            return;
        }

        temblorRestante = Mathf.Max(0f, temblorRestante - Time.deltaTime);
        float fuerza = temblorFuerza * temblorRestante / Mathf.Max(0.001f, temblorDuracion);
        float t = Time.time * 35f;
        Vector2 salto = new Vector2(
            Mathf.PerlinNoise(t, 11f) * 2f - 1f,
            Mathf.PerlinNoise(23f, t) * 2f - 1f) * fuerza;
        // Desplaza la imagen, sin mover el Transform ni las barreras hijas.
        Matrix4x4 matriz = camara.projectionMatrix;
        matriz.m03 += salto.x / Mathf.Max(0.01f, camara.orthographicSize * camara.aspect);
        matriz.m13 += salto.y / Mathf.Max(0.01f, camara.orthographicSize);
        camara.projectionMatrix = matriz;
        proyeccionTemblor = true;
    }

    private bool BarrerasValidas()
    {
        return barreraIzquierda != null && barreraDerecha != null &&
            barreraIzquierda != barreraDerecha &&
            barreraIzquierda.gameObject != barreraDerecha.gameObject &&
            barreraIzquierda.gameObject != gameObject &&
            barreraDerecha.gameObject != gameObject &&
            barreraIzquierda.transform.IsChildOf(transform) &&
            barreraDerecha.transform.IsChildOf(transform) &&
            barreraIzquierda.attachedRigidbody == null &&
            barreraDerecha.attachedRigidbody == null &&
            barreraIzquierda.gameObject.activeInHierarchy &&
            barreraDerecha.gameObject.activeInHierarchy;
    }

    private void ActualizarBarreras()
    {
        if (camara == null || !camara.orthographic || !BarrerasValidas())
            return;

        float alto = camara.orthographicSize * 2f;
        float ancho = alto * camara.aspect;
        float grosor = Mathf.Max(0.1f, grosorBarreras);
        float margen = Mathf.Clamp(margenInterior, 0f, ancho * 0.25f);
        float altura = alto + Mathf.Max(0f, extensionVertical) * 2f;
        Vector3 centro = transform.position;
        float planoZ = objetivo != null ? objetivo.position.z : 0f;

        AjustarBarrera(barreraIzquierda,
            new Vector3(centro.x - ancho * 0.5f + margen - grosor * 0.5f,
                centro.y, planoZ), grosor, altura);
        AjustarBarrera(barreraDerecha,
            new Vector3(centro.x + ancho * 0.5f - margen + grosor * 0.5f,
                centro.y, planoZ), grosor, altura);
    }

    private void AjustarBarrera(
        BoxCollider2D barrera, Vector3 posicion, float ancho, float alto)
    {
        barrera.transform.rotation = Quaternion.identity;
        Vector3 escala = barrera.transform.lossyScale;
        barrera.offset = Vector2.zero;
        barrera.size = new Vector2(
            ancho / Mathf.Max(0.0001f, Mathf.Abs(escala.x)),
            alto / Mathf.Max(0.0001f, Mathf.Abs(escala.y)));
        barrera.transform.position = posicion;
        barrera.isTrigger = false;
        barrera.enabled = true;
    }

    private void OnDisable()
    {
        if (camara != null && proyeccionTemblor)
            camara.ResetProjectionMatrix();
        proyeccionTemblor = false;
        temblorRestante = 0f;
        temblorFuerza = 0f;
        if (barreraIzquierda != null && barreraIzquierda.gameObject != gameObject)
            barreraIzquierda.enabled = false;
        if (barreraDerecha != null && barreraDerecha.gameObject != gameObject)
            barreraDerecha.enabled = false;
    }
}
