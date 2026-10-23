using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(VidaGusano))]
public class GusanoModularPrueba : MonoBehaviour
{
    [Header("Imagenes")]
    public Texture2D hojaPiezas;
    [Tooltip("Opcional: un sprite ORIGINAL del chasis para copiar sus PPU.")]
    public Sprite referenciaExcavadora;
    [Min(1f)] public float pixelsPorUnidad = 100f;
    [Min(0.1f)] public float tamano = 1f;
    public bool bocaAbierta;
    public Material materialOpcional;

    [Header("Cuerpo")]
    [Range(3, 30)] public int cantidadSegmentos = 9;

    [Header("Recorrido: desde entrada hasta salida")]
    public Transform[] puntosRecorrido;
    [Min(0.01f)] public float velocidad = 4f;
    [Range(8, 100)] public int muestrasPorTramo = 40;
    public bool iniciarAutomaticamente = true;
    public bool repetir = true;
    [Min(0f)] public float pausaEntrePasadas = 2f;
    public bool pausar;

    [Header("Momento de enterrarse")]
    [Tooltip("Indice de Puntos Recorrido que marca el entierro. Elemento 2 es el tercer punto.")]
    [Min(1)] public int puntoEnterrado = 2;
    public event System.Action AlEnterrarse;
    public event System.Action PruebaDetenida;

    [Header("Orden de dibujo")]
    public string sortingLayer = "Default";
    public int ordenBase = 20;

    private readonly List<Vector2> curva = new List<Vector2>();
    private readonly List<float> distancias = new List<float>();
    private readonly List<SpriteRenderer> piezas = new List<SpriteRenderer>();
    private readonly Sprite[] sprites = new Sprite[10];
    private Transform contenedor;
    private VidaGusano vida;
    private float largoCurva;
    private float avance;
    private float avanceFinal;
    private float paso;
    private float offsetExtremos;
    private float espera;
    private bool listo;
    private bool enMarcha;
    private bool esperando;
    private float distanciaEntierro;
    private bool entierroAvisado;
    private SistemaAtaquesGusano sistema;

    public Vector2 DireccionEntierro { get; private set; } = Vector2.down;
    public Vector2 PuntoEntierro { get; private set; }
    public float DiametroCuerpo => paso * 212f / 112f;
    public float LongitudVisual => (piezas.Count + 2) * paso + offsetExtremos * 2f;
    public float LongitudRuta => largoCurva;
    public SpriteRenderer ImagenCabeza => piezas.Count > 0 ? piezas[0] : null;

    public void EstablecerBoca(bool abierta)
    {
        bocaAbierta = abierta;
        if (listo && curva.Count > 1)
            ActualizarCuerpo();
    }

    public bool ObtenerBoca(out Vector2 posicion, out Vector2 direccion)
    {
        posicion = transform.position;
        direccion = Vector2.left;
        if (!listo || piezas.Count == 0 || piezas[0].sprite == null)
            return false;
        Transform cabeza = piezas[0].transform;
        float ppu = piezas[0].sprite.pixelsPerUnit;
        posicion = cabeza.TransformPoint(new Vector3(-160f / ppu, 0f, 0f));
        direccion = cabeza.TransformDirection(Vector3.left).normalized;
        return true;
    }

    public bool Preparado => listo;

    public void DetenerPrueba()
    {
        if (sistema != null)
            sistema.DetenerCombate();
        enMarcha = false;
        esperando = false;
        Mostrar(false);
        PruebaDetenida?.Invoke();
    }

    private void Start()
    {
        sistema = GetComponent<SistemaAtaquesGusano>();
        vida = GetComponent<VidaGusano>();
        if (vida == null)
            vida = gameObject.AddComponent<VidaGusano>();
        vida.Murio += PararPorMuerte;
        if (hojaPiezas == null ||
            hojaPiezas.width != 2048 || hojaPiezas.height != 2048)
        {
            Fallar("Asigna la hoja completa del gusano de 2048 x 2048.");
            return;
        }

        if ((transform.lossyScale - Vector3.one).sqrMagnitude > 0.0001f)
        {
            Fallar("Deja la escala del objeto y sus padres en 1,1,1. Usa Tamano.");
            return;
        }

        if (sistema == null || !sistema.enabled)
        {
            if (!ConstruirRecorrido())
                return;
        }

        float ppu = referenciaExcavadora != null
            ? referenciaExcavadora.pixelsPerUnit
            : Mathf.Max(1f, pixelsPorUnidad);

        float escala = Mathf.Max(0.1f, tamano);
        paso = 112f / ppu * escala;
        offsetExtremos = 128f / ppu * escala;

        // Cortes fijos: no depende de los nombres ni del Slice del importador.
        for (int i = 0; i < sprites.Length; i++)
        {
            Rect rect = new Rect(
                (i % 4) * 512,
                2048 - (i / 4 + 1) * 512,
                512, 512
            );
            sprites[i] = Sprite.Create(
                hojaPiezas, rect, new Vector2(0.5f, 0.5f),
                ppu, 0, SpriteMeshType.FullRect
            );
            sprites[i].name = "Gusano_Pieza_" + i;
        }

        contenedor = new GameObject("Visual_Gusano_Prueba").transform;
        contenedor.SetParent(transform, false);
        int cantidad = Mathf.Clamp(cantidadSegmentos, 3, 30);
        CrearPieza("Cabeza", sprites[bocaAbierta ? 1 : 0], escala);
        CrearPieza("Cuello", sprites[5], escala);

        int[] patron = { 6, 7, 6, 8 };
        for (int i = 0; i < cantidad; i++)
            CrearPieza("Cuerpo_" + i, sprites[patron[i % patron.Length]], escala);

        CrearPieza("Cola", sprites[9], escala);

        // La cabeza tapa el cuello; cada modulo tapa el empalme posterior.
        for (int i = 0; i < piezas.Count; i++)
            piezas[i].sortingOrder = ordenBase + piezas.Count - 1 - i;

        // Sigue avanzando hasta que tambien la punta de la cola sale.
        avanceFinal = largoCurva + (cantidad + 1) * paso + 384f / ppu * escala;
        listo = true;
        Mostrar(false);
        if (iniciarAutomaticamente)
            IniciarPasada();
    }

    private void CrearPieza(string nombre, Sprite sprite, float escala)
    {
        GameObject objeto = new GameObject(nombre);
        objeto.layer = gameObject.layer;
        objeto.transform.SetParent(contenedor, false);
        objeto.transform.localScale = Vector3.one * escala;
        SpriteRenderer renderer = objeto.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = sortingLayer;
        if (materialOpcional != null)
            renderer.sharedMaterial = materialOpcional;
        piezas.Add(renderer);
        vida.CrearZonasPieza(objeto.transform,
            System.Array.IndexOf(sprites, sprite), sprite.pixelsPerUnit);
    }

    private bool ConstruirRecorrido()
    {
        if (puntosRecorrido == null || puntosRecorrido.Length < 2)
        {
            Fallar("Asigna al menos dos Puntos Recorrido, en orden.");
            return false;
        }

        List<Vector2> nodos = new List<Vector2>();
        for (int i = 0; i < puntosRecorrido.Length; i++)
        {
            if (puntosRecorrido[i] == null)
            {
                Fallar("Falta asignar el punto de recorrido " + i + ".");
                return false;
            }

            Vector2 punto = puntosRecorrido[i].position;
            if (nodos.Count == 0 ||
                (punto - nodos[nodos.Count - 1]).sqrMagnitude > 0.000001f)
                nodos.Add(punto);
        }

        if (nodos.Count < 2)
        {
            Fallar("Los puntos deben estar en posiciones diferentes.");
            return false;
        }

        AgregarMuestra(nodos[0]);
        int muestras = Mathf.Clamp(muestrasPorTramo, 8, 100);
        int indiceEntierro = Mathf.Clamp(puntoEnterrado, 1, nodos.Count - 1);
        for (int i = 0; i < nodos.Count - 1; i++)
        {
            Vector2 p1 = nodos[i];
            Vector2 p2 = nodos[i + 1];
            Vector2 p0 = i > 0 ? nodos[i - 1] : 2f * p1 - p2;
            Vector2 p3 = i + 2 < nodos.Count ? nodos[i + 2] : 2f * p2 - p1;
            for (int j = 1; j <= muestras; j++)
                AgregarMuestra(Catmull(p0, p1, p2, p3, (float)j / muestras));
            if (i + 1 == indiceEntierro)
                distanciaEntierro = distancias[distancias.Count - 1];
        }

        largoCurva = distancias[distancias.Count - 1];
        return true;
    }

    private void AgregarMuestra(Vector2 punto)
    {
        float distancia = 0f;
        if (curva.Count > 0)
        {
            float tramo = Vector2.Distance(curva[curva.Count - 1], punto);
            if (tramo < 0.00001f)
                return;
            distancia = distancias[distancias.Count - 1] + tramo;
        }
        curva.Add(punto);
        distancias.Add(distancia);
    }

    private static Vector2 Catmull(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        return 0.5f * ((2f * b) + (-a + c) * t +
            (2f * a - 5f * b + 4f * c - d) * t * t +
            (-a + 3f * b - 3f * c + d) * t * t * t);
    }

    // Se busca por distancia recorrida, no por tiempo entre seguidores.
    // Asi el cuerpo conserva su separacion aunque cambie la velocidad.
    private Vector2 PuntoEnCurva(float distancia)
    {
        int ultimo = curva.Count - 1;
        if (distancia <= 0f)
            return curva[0] + (curva[1] - curva[0]).normalized * distancia;
        if (distancia >= largoCurva)
            return curva[ultimo] + (curva[ultimo] - curva[ultimo - 1]).normalized *
                (distancia - largoCurva);

        int izquierda = 0;
        int derecha = ultimo;
        while (derecha - izquierda > 1)
        {
            int centro = (izquierda + derecha) / 2;
            if (distancias[centro] < distancia)
                izquierda = centro;
            else
                derecha = centro;
        }
        float t = Mathf.InverseLerp(
            distancias[izquierda], distancias[derecha], distancia);
        return Vector2.Lerp(curva[izquierda], curva[derecha], t);
    }

    private void Update()
    {
        if (sistema != null && sistema.enabled)
            return;
        if (!listo || pausar)
            return;

        if (esperando)
        {
            espera -= Time.deltaTime;
            if (espera <= 0f)
                IniciarPasada();
            return;
        }

        if (!enMarcha)
            return;

        avance = Mathf.Min(avanceFinal,
            avance + Mathf.Max(0.01f, velocidad) * Time.deltaTime);
        ActualizarCuerpo();
        if (!entierroAvisado && avance >= distanciaEntierro)
        {
            entierroAvisado = true;
            AlEnterrarse?.Invoke();
        }
        if (avance >= avanceFinal)
        {
            enMarcha = false;
            Mostrar(false);
            esperando = repetir;
            espera = Mathf.Max(0f, pausaEntrePasadas);
        }
    }

    [ContextMenu("Reiniciar pasada (en Play)")]
    public void IniciarPasada()
    {
        if (!listo || !isActiveAndEnabled || !vida.EstaVivo)
            return;
        if (sistema != null && sistema.enabled)
        {
            sistema.IniciarCombate();
            return;
        }
        avance = 0f;
        entierroAvisado = false;
        esperando = false;
        enMarcha = true;
        Mostrar(true);
        ActualizarCuerpo();
    }

    private void ActualizarCuerpo()
    {
        Sprite cabeza = sprites[bocaAbierta ? 1 : 0];
        if (piezas[0].sprite != cabeza)
        {
            piezas[0].sprite = cabeza;
            vida.ActualizarZonasCabeza(piezas[0].transform,
                bocaAbierta ? 1 : 0, cabeza.pixelsPerUnit);
        }
        Vector2 unionCabeza = PuntoEnCurva(avance);
        Vector2 direccion = (PuntoEnCurva(avance + paso * 0.5f) - unionCabeza).normalized;
        Colocar(piezas[0], unionCabeza + direccion * offsetExtremos, direccion);

        int modulos = piezas.Count - 2; // Cuello y todos los segmentos.
        for (int i = 0; i < modulos; i++)
        {
            Vector2 frente = PuntoEnCurva(avance - i * paso);
            Vector2 fondo = PuntoEnCurva(avance - (i + 1) * paso);
            Colocar(piezas[i + 1], (frente + fondo) * 0.5f, (frente - fondo).normalized);
        }

        float distanciaCola = avance - modulos * paso;
        Vector2 unionCola = PuntoEnCurva(distanciaCola);
        Vector2 direccionCola = (PuntoEnCurva(distanciaCola + paso * 0.5f) - unionCola).normalized;
        Colocar(piezas[piezas.Count - 1],
            unionCola - direccionCola * offsetExtremos, direccionCola);
    }

    private void Colocar(SpriteRenderer pieza, Vector2 centro, Vector2 direccion)
    {
        float angulo = Mathf.Atan2(direccion.y, direccion.x) * Mathf.Rad2Deg + 180f;
        pieza.transform.SetPositionAndRotation(
            new Vector3(centro.x, centro.y, transform.position.z),
            Quaternion.Euler(0f, 0f, angulo));
    }

    // Las muestras las genera el sistema de ataques, no son puntos del Inspector.
    public void ConfigurarRutaAtaque(IList<Vector2> muestras)
    {
        if (!listo || muestras == null || muestras.Count < 2)
            return;
        Mostrar(false);
        curva.Clear();
        distancias.Clear();
        foreach (Vector2 muestra in muestras)
            AgregarMuestra(muestra);
        largoCurva = distancias[distancias.Count - 1];
        avance = 0f;
        enMarcha = false;
        esperando = false;
    }

    public void ColocarCabezaEnRuta(float distancia)
    {
        if (!listo || curva.Count < 2)
            return;
        avance = Mathf.Clamp(distancia, 0f, largoCurva);
        ActualizarCuerpo();
        Mostrar(true);
    }

    public void OcultarAtaque() => Mostrar(false);

    public void AvisarEntierro(Vector2 punto, Vector2 direccion)
    {
        PuntoEntierro = punto;
        DireccionEntierro = direccion.normalized;
        AlEnterrarse?.Invoke();
    }

    private void Mostrar(bool valor)
    {
        if (vida != null)
        {
            valor = valor && vida.EstaVivo;
            vida.EstablecerExpuesto(valor);
        }
        foreach (SpriteRenderer pieza in piezas)
            if (pieza != null)
                pieza.enabled = valor;
    }

    private void PararPorMuerte()
    {
        enMarcha = false;
        esperando = false;
        enabled = false;
    }

    private void Fallar(string mensaje)
    {
        Debug.LogError("GusanoModularPrueba: " + mensaje, this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (listo)
            Mostrar(enMarcha);
    }

    private void OnDisable()
    {
        if (sistema != null)
            sistema.DetenerCombate();
        Mostrar(false);
        PruebaDetenida?.Invoke();
    }

    private void OnDestroy()
    {
        if (vida != null)
            vida.Murio -= PararPorMuerte;
        if (contenedor != null)
            Destroy(contenedor.gameObject);
        foreach (Sprite sprite in sprites)
            if (sprite != null)
                Destroy(sprite);
    }

    private void OnDrawGizmosSelected()
    {
        if (puntosRecorrido == null)
            return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < puntosRecorrido.Length; i++)
        {
            if (puntosRecorrido[i] == null)
                continue;
            Gizmos.DrawWireSphere(puntosRecorrido[i].position, 0.15f);
            if (i > 0 && puntosRecorrido[i - 1] != null)
                Gizmos.DrawLine(puntosRecorrido[i - 1].position, puntosRecorrido[i].position);
        }
    }
}
