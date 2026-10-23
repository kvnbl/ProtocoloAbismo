using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GusanoModularPrueba))]
public class AtaqueRocasGusano : MonoBehaviour
{
    [Header("Referencias de la sala")]
    public ControlCamara controlCamara;
    public VidaJugador jugador;
    public TaladroTermico taladro;
    public Transform techoIzquierdo;
    public Transform techoDerecho;
    public LayerMask capasSuelo;

    [Header("Rocas e imagenes")]
    public RocaJefe prefabRoca;
    public Sprite[] variantes;
    public Texture2D hojaEfectos;
    [Range(0.25f, 2f)] public float escalaMinima = 0.9f;
    [Range(0.25f, 2f)] public float escalaMaxima = 1.1f;

    [Header("Aviso y caida")]
    [Range(1, 12)] public int cantidadRocas = 4;
    [Min(0.2f)] public float tiempoAviso = 0.8f;
    [Min(0.05f)] public float intervaloRocas = 0.3f;
    [Min(0f)] public float separacionMinima = 0.3f;
    [Min(0f)] public float margenBorde = 0.5f;

    [Header("Hueco para esquivar")]
    [Tooltip("Ancho extra sobre el ancho real de los colliders de la excavadora.")]
    [Min(0.1f)] public float margenHueco = 0.6f;
    [Tooltip("Cuanto puede alejarse el centro del hueco de la excavadora al avisar.")]
    [Min(0f)] public float desplazamientoHueco = 1.5f;

    [Header("Temblor breve")]
    [Min(0f)] public float duracionTemblor = 0.25f;
    [Min(0f)] public float fuerzaTemblor = 0.08f;

    [Header("Dos rocas desde una pared")]
    [Min(0.1f)] public float velocidadLateral = 5f;
    [Min(0.1f)] public float impulsoArriba = 6f;

    [Header("Visibilidad y diagnostico")]
    public string sortingLayerRocas = "Efectos";
    public int ordenRocas = 100;
    [SerializeField] private string ultimoPatron;
    [SerializeField] private int rocasLanzadas;

    private GusanoModularPrueba gusano;
    private VidaGusano vida;
    private bool atacando;
    private bool errorMostrado;
    private readonly List<GameObject> avisos = new List<GameObject>();
    private readonly List<RocaJefe> rocas = new List<RocaJefe>();
    public bool AtaqueEnCurso => atacando || rocas.Exists(roca => roca != null);

    private void OnEnable()
    {
        gusano = GetComponent<GusanoModularPrueba>();
        vida = GetComponent<VidaGusano>();
        gusano.AlEnterrarse += LanzarAtaque;
        gusano.PruebaDetenida += CancelarAtaque;
        if (vida != null)
            vida.Murio += CancelarAtaque;
    }

    private void OnDisable()
    {
        if (gusano != null)
        {
            gusano.AlEnterrarse -= LanzarAtaque;
            gusano.PruebaDetenida -= CancelarAtaque;
        }
        if (vida != null)
            vida.Murio -= CancelarAtaque;
        CancelarAtaque();
    }

    private bool PrepararReferencias(bool necesitaTecho)
    {
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        if (jugador == null && controlCamara != null && controlCamara.objetivo != null)
            jugador = controlCamara.objetivo.GetComponentInParent<VidaJugador>();
        if (taladro == null && jugador != null)
            taladro = jugador.GetComponentInChildren<TaladroTermico>();

        bool valido = controlCamara != null && controlCamara.CamaraJuego != null &&
            jugador != null && taladro != null && taladro.zonaDanio != null &&
            prefabRoca != null && prefabRoca.Imagen.sprite != null &&
            (!necesitaTecho || (techoIzquierdo != null && techoDerecho != null &&
            Mathf.Abs(techoDerecho.position.x - techoIzquierdo.position.x) > 0.1f)) &&
            capasSuelo.value != 0 && hojaEfectos != null &&
            hojaEfectos.width == 512 && hojaEfectos.height == 384;
        if (!valido && !errorMostrado)
        {
            Debug.LogError("AtaqueRocasGusano: asigna camara, VidaJugador, TaladroTermico " +
                "con Zona Danio, prefab RocaJefe con sprite, dos puntos separados bajo el " +
                "techo, Capas Suelo y la hoja completa de 512 x 384. Consulta el LEEME.", this);
            errorMostrado = true;
        }
        return valido;
    }

    private void LanzarAtaque()
    {
        bool lateral = Mathf.Abs(gusano.DireccionEntierro.x) > 0.5f;
        if (!isActiveAndEnabled || atacando || !PrepararReferencias(!lateral) ||
            !controlCamara.EnCombate || controlCamara.EncuadreEnTransicion ||
            (vida != null && !vida.EstaVivo))
            return;
        Vector2 direccion = gusano.DireccionEntierro;
        ultimoPatron = lateral ? (direccion.x > 0f ? "Pared derecha" : "Pared izquierda") : "Techo";
        List<Vector3> posiciones = lateral ? PlanearLateral(direccion) : PlanearCaida();
        if (posiciones.Count == 0)
        {
            Debug.LogWarning(lateral
                ? "Rocas laterales: no hay espacio libre junto a la salida. Coloca las madrigueras " +
                  "en la cara INTERIOR de la pared y comprueba Capas Suelo y el tamano del prefab."
                : "Rocas del techo: no caben dejando el hueco de la excavadora. Amplia los " +
                  "puntos del techo o reduce la escala del prefab.", this);
            return;
        }
        atacando = true;
        rocas.RemoveAll(roca => roca == null);
        StartCoroutine(CaerRocas(posiciones, lateral, direccion));
    }

    private Bounds LimitesJugador()
    {
        Bounds limites = new Bounds(jugador.transform.position, new Vector3(1f, 1f, 0f));
        bool primero = true;
        foreach (Collider2D collider in jugador.GetComponentsInChildren<Collider2D>())
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy)
                continue;
            if (primero)
            {
                limites = collider.bounds;
                primero = false;
            }
            else
                limites.Encapsulate(collider.bounds);
        }
        return limites;
    }

    private List<Vector3> PlanearCaida()
    {
        List<Vector3> resultado = new List<Vector3>();
        Camera camara = controlCamara.CamaraJuego;
        float mitadAncho = camara.orthographicSize * camara.aspect;
        Vector3 a = techoIzquierdo.position;
        Vector3 b = techoDerecho.position;
        if (a.x > b.x)
        {
            Vector3 intercambio = a;
            a = b;
            b = intercambio;
        }
        float radio = prefabRoca.RadioEstimado(null, Mathf.Max(escalaMinima, escalaMaxima));
        if (variantes != null)
            foreach (Sprite sprite in variantes)
                if (sprite != null)
                    radio = Mathf.Max(radio, prefabRoca.RadioEstimado(sprite,
                        Mathf.Max(escalaMinima, escalaMaxima)));

        float izquierda = Mathf.Max(a.x, camara.transform.position.x - mitadAncho + margenBorde);
        float derecha = Mathf.Min(b.x, camara.transform.position.x + mitadAncho - margenBorde);
        Bounds jugadorBounds = LimitesJugador();
        float anchoHueco = jugadorBounds.size.x + Mathf.Max(0.1f, margenHueco);
        // Hueco completo dentro del tramo visible, con centros fuera de sus bordes.
        if (derecha - izquierda < anchoHueco + radio * 2f)
            return resultado;
        float centroMinimo = izquierda + anchoHueco * 0.5f;
        float centroMaximo = derecha - anchoHueco * 0.5f;
        float cercaMinimo = Mathf.Max(centroMinimo, jugadorBounds.center.x - desplazamientoHueco);
        float cercaMaximo = Mathf.Min(centroMaximo, jugadorBounds.center.x + desplazamientoHueco);
        // No obliga a cruzar la sala si los marcadores quedaron lejos del jugador.
        if (cercaMinimo > cercaMaximo)
            return resultado;
        float centro = Random.Range(cercaMinimo, cercaMaximo);
        float huecoIzquierdo = centro - anchoHueco * 0.5f;
        float huecoDerecho = centro + anchoHueco * 0.5f;
        float paso = Mathf.Max(0.05f, radio * 2f + separacionMinima);
        List<float> candidatos = new List<float>();
        for (float x = izquierda + radio; x <= derecha - radio + 0.0001f; x += paso)
        {
            if (x + radio <= huecoIzquierdo || x - radio >= huecoDerecho)
                candidatos.Add(x);
        }
        // El orden de caida cambia; las posiciones ya no persiguen al jugador.
        for (int i = candidatos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            float temporal = candidatos[i];
            candidatos[i] = candidatos[j];
            candidatos[j] = temporal;
        }
        int cantidad = Mathf.Min(Mathf.Clamp(cantidadRocas, 1, 12), candidatos.Count);
        for (int i = 0; i < cantidad; i++)
        {
            float x = candidatos[i];
            float y = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, x));
            y = Mathf.Min(y, camara.transform.position.y + camara.orthographicSize - radio - 0.1f);
            if (y - radio <= jugadorBounds.max.y + 0.3f)
                continue;
            resultado.Add(new Vector3(x, y, jugador.transform.position.z));
        }
        return resultado;
    }

    private List<Vector3> PlanearLateral(Vector2 haciaFuera)
    {
        List<Vector3> posiciones = new List<Vector3>();
        Camera camara = controlCamara.CamaraJuego;
        float radio = prefabRoca.RadioEstimado(null, Mathf.Max(escalaMinima, escalaMaxima));
        if (variantes != null)
            foreach (Sprite sprite in variantes)
                if (sprite != null)
                    radio = Mathf.Max(radio, prefabRoca.RadioEstimado(sprite,
                        Mathf.Max(escalaMinima, escalaMaxima)));
        float semiancho = camara.orthographicSize * camara.aspect;
        float izquierda = camara.transform.position.x - semiancho + margenBorde + radio;
        float derecha = camara.transform.position.x + semiancho - margenBorde - radio;
        float abajo = camara.transform.position.y - camara.orthographicSize + radio;
        float arriba = camara.transform.position.y + camara.orthographicSize - radio - 0.1f;
        if (izquierda >= derecha || abajo >= arriba)
            return posiciones;
        Vector2 origen = gusano.PuntoEntierro;
        float x = Mathf.Clamp(origen.x - Mathf.Sign(haciaFuera.x) * (radio + 0.15f), izquierda, derecha);
        float y = Mathf.Clamp(origen.y + radio * 2.5f, abajo, arriba);
        Vector2 libre;
        if (!UtilidadAtaquesJefe.BuscarSalida(new Vector2(x, y), -haciaFuera,
            radio, capasSuelo, camara, out libre))
            return posiciones;
        for (int i = 0; i < 2; i++)
            posiciones.Add(new Vector3(libre.x, libre.y, jugador.transform.position.z));
        return posiciones;
    }

    private IEnumerator CaerRocas(List<Vector3> posiciones, bool lateral, Vector2 haciaFuera)
    {
        SpriteRenderer referencia = prefabRoca.Imagen;
        float anchoEfecto = prefabRoca.RadioEstimado(null, 1f) * 3f;
        for (int i = 0; i < posiciones.Count; i++)
        {
            GameObject aviso = EfectoRocaJefe.Crear(hojaEfectos, 1, posiciones[i], anchoEfecto,
                referencia, Mathf.Max(0.2f, tiempoAviso) + i * Mathf.Max(0.05f, intervaloRocas) + 0.1f, true);
            avisos.Add(aviso);
            if (aviso != null)
                UtilidadAtaquesJefe.Ordenar(aviso.GetComponent<SpriteRenderer>(), sortingLayerRocas, ordenRocas + 3);
        }
        controlCamara.Sacudir(duracionTemblor, fuerzaTemblor);
        yield return new WaitForSeconds(Mathf.Max(0.2f, tiempoAviso));
        for (int i = 0; i < posiciones.Count; i++)
        {
            if (avisos[i] != null)
                Destroy(avisos[i]);
            RocaJefe roca = Instantiate(prefabRoca, posiciones[i],
                Quaternion.Euler(0f, 0f, Random.Range(-20f, 20f)));
            float escala = Random.Range(Mathf.Min(escalaMinima, escalaMaxima),
                Mathf.Max(escalaMinima, escalaMaxima));
            roca.transform.localScale *= escala;
            UtilidadAtaquesJefe.Ordenar(roca.Imagen, sortingLayerRocas, ordenRocas);
            Sprite variante = variantes != null && variantes.Length > 0
                ? variantes[Random.Range(0, variantes.Length)] : null;
            Vector2 velocidadInicial = lateral
                ? new Vector2(-Mathf.Sign(haciaFuera.x) * velocidadLateral * (1f + i * 0.35f),
                    impulsoArriba * (i == 0 ? 0.85f : 1.15f))
                : Vector2.zero;
            roca.Inicializar(velocidadInicial, jugador, taladro, capasSuelo, hojaEfectos, variante);
            rocas.Add(roca);
            rocasLanzadas++;
            if (i + 1 < posiciones.Count)
                yield return new WaitForSeconds(Mathf.Max(0.05f, intervaloRocas));
        }
        avisos.Clear();
        atacando = false;
    }

    public void CancelarAtaque()
    {
        StopAllCoroutines();
        foreach (GameObject aviso in avisos)
            if (aviso != null)
                Destroy(aviso);
        foreach (RocaJefe roca in rocas)
            if (roca != null)
                Destroy(roca.gameObject);
        avisos.Clear();
        rocas.Clear();
        atacando = false;
    }

    [ContextMenu("Probar rocas pared derecha (en Play y dentro de la sala)")]
    private void ProbarDerecha() => ProbarPared(true);

    [ContextMenu("Probar rocas pared izquierda (en Play y dentro de la sala)")]
    private void ProbarIzquierda() => ProbarPared(false);

    private void ProbarPared(bool derecha)
    {
        if (!Application.isPlaying)
            return;
        SistemaAtaquesGusano sistema = GetComponent<SistemaAtaquesGusano>();
        Transform salida = sistema != null ? (derecha ? sistema.paredDerecha : sistema.paredIzquierda) : null;
        if (salida == null || controlCamara == null || !controlCamara.EnCombate || atacando)
        {
            Debug.LogWarning("Prueba de rocas: entra en la sala, asigna la pared y espera a que termine el aviso actual.", this);
            return;
        }
        gusano.AvisarEntierro(salida.position, derecha ? Vector2.right : Vector2.left);
    }
}
