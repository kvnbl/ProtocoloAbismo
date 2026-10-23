using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GusanoModularPrueba))]
public class AtaqueAcidoGusano : MonoBehaviour
{
    [Header("Referencias")]
    public Texture2D hojaAcido;
    public VidaJugador jugador;
    public ControlCamara controlCamara;
    public LayerMask capasSuelo;

    [Header("Disparo desde la boca")]
    [Range(1, 3)] public int cantidadDisparos = 2;
    [Min(0.3f)] public float avisoBoca = 0.8f;
    [Min(0.15f)] public float intervaloDisparos = 0.3f;
    [Min(0.1f)] public float gravedad = 9f;
    [Min(0.1f)] public float velocidadReferencia = 8f;
    [Min(1)] public int danio = 1;
    [Range(0.2f, 1f)] public float tamanoRelativo = 0.55f;

    [Header("Dibujo")]
    public string sortingLayerAcido = "Efectos";
    public int ordenAcido = 120;
    [Tooltip("Opcional: si existe esta Layer fisica, se usa para los objetos creados.")]
    public string capaFisica = "AcidoJefe";
    [SerializeField] private int disparosLanzados;

    private GusanoModularPrueba gusano;
    private VidaGusano vida;
    private readonly List<ProyectilAcidoGusano> proyectiles = new List<ProyectilAcidoGusano>();
    private GameObject carga;
    private bool cancelado;
    private bool avisoMostrado;

    private void Awake()
    {
        gusano = GetComponent<GusanoModularPrueba>();
    }

    private void OnEnable()
    {
        vida = GetComponent<VidaGusano>();
        if (vida != null)
            vida.Murio += CancelarAtaque;
    }

    private void OnDisable()
    {
        if (vida != null)
            vida.Murio -= CancelarAtaque;
        CancelarAtaque();
    }

    private bool Preparar()
    {
        AtaqueRocasGusano rocas = GetComponent<AtaqueRocasGusano>();
        if (controlCamara == null && rocas != null)
            controlCamara = rocas.controlCamara;
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        if (jugador == null && rocas != null)
            jugador = rocas.jugador;
        if (jugador == null && controlCamara != null && controlCamara.objetivo != null)
            jugador = controlCamara.objetivo.GetComponentInParent<VidaJugador>();
        if (capasSuelo.value == 0 && rocas != null)
            capasSuelo = rocas.capasSuelo;
        bool valido = jugador != null && capasSuelo.value != 0 &&
            hojaAcido != null && hojaAcido.width == 512 && hojaAcido.height == 256;
        if (!valido && !avisoMostrado)
        {
            avisoMostrado = true;
            Debug.LogError("AtaqueAcidoGusano: asigna la hoja completa de 512 x 256, " +
                "VidaJugador y Capas Suelo. No necesita prefab ni Animator.", this);
        }
        return valido;
    }

    public IEnumerator Escupir()
    {
        cancelado = false;
        if (!isActiveAndEnabled || !Preparar())
            yield break;
        proyectiles.RemoveAll(proyectil => proyectil == null);
        gusano.EstablecerBoca(true);
        Vector2 origen;
        Vector2 direccion;
        if (!gusano.ObtenerBoca(out origen, out direccion))
        {
            CerrarBoca();
            yield break;
        }
        float ancho = Mathf.Max(0.1f, gusano.DiametroCuerpo * tamanoRelativo);
        Vector2 libre;
        Camera camara = controlCamara != null ? controlCamara.CamaraJuego : null;
        if (!UtilidadAtaquesJefe.BuscarSalida(origen, direccion, ancho * 0.29f,
            capasSuelo, camara, out libre))
        {
            Debug.LogWarning("Acido: la boca esta dentro del terreno o fuera del encuadre. " +
                "Revisa la madriguera de la pared y Altura Asomarse.", this);
            CerrarBoca();
            yield break;
        }
        origen = libre;
        Collider2D chasis = jugador.GetComponent<Collider2D>();
        Vector2 objetivo = chasis != null ? (Vector2)chasis.bounds.center : (Vector2)jugador.transform.position;
        if (Vector2.Dot(objetivo - origen, direccion) <= 0f)
            objetivo = origen + direccion * gusano.DiametroCuerpo * 3f + Vector2.down * gusano.DiametroCuerpo;
        // Se apunta una sola vez ANTES del aviso. No persigue al jugador durante la rafaga.
        float vuelo = Mathf.Clamp(Vector2.Distance(origen, objetivo) / Mathf.Max(0.1f, velocidadReferencia), 0.7f, 1.5f);
        Vector2 impulso = CalcularImpulso(origen, objetivo, gravedad, vuelo);
        Vector3 posicion = new Vector3(origen.x, origen.y, transform.position.z);
        carga = VisualAcidoGusano.CrearEfecto(hojaAcido, 0, posicion,
            ancho * 0.45f, avisoBoca, true, sortingLayerAcido, ordenAcido);
        float tiempo = 0f;
        while (tiempo < Mathf.Max(0.3f, avisoBoca))
        {
            if (Interrumpido())
            {
                CerrarBoca();
                yield break;
            }
            if (!gusano.pausar)
                tiempo += Time.deltaTime;
            if (carga != null)
                carga.transform.localScale = Vector3.one * ancho *
                    Mathf.Lerp(0.3f, 0.65f, Mathf.Clamp01(tiempo / Mathf.Max(0.3f, avisoBoca)));
            yield return null;
        }
        if (carga != null)
            Destroy(carga);
        carga = null;
        for (int i = 0; i < Mathf.Clamp(cantidadDisparos, 1, 3); i++)
        {
            if (Interrumpido())
                break;
            GameObject objeto = new GameObject("Acido_Gusano");
            objeto.transform.position = posicion;
            objeto.transform.localScale = Vector3.one * ancho;
            int layer = LayerMask.NameToLayer(capaFisica);
            if (layer >= 0)
                objeto.layer = layer;
            ProyectilAcidoGusano proyectil = objeto.AddComponent<ProyectilAcidoGusano>();
            proyectil.Inicializar(hojaAcido, impulso, gravedad, danio, jugador,
                capasSuelo, sortingLayerAcido, ordenAcido);
            proyectiles.Add(proyectil);
            AudioJuegoSimple.Instancia.GusanoEscupir();
            disparosLanzados++;
            float espera = i + 1 < cantidadDisparos ? Mathf.Max(0.15f, intervaloDisparos) : 0.3f;
            float pasado = 0f;
            while (pasado < espera && !Interrumpido())
            {
                if (!gusano.pausar)
                    pasado += Time.deltaTime;
                yield return null;
            }
        }
        CerrarBoca();
    }

    public static Vector2 CalcularImpulso(Vector2 origen, Vector2 destino, float gravedad, float segundos)
    {
        float tiempo = Mathf.Max(0.1f, segundos);
        return (destino - origen) / tiempo + Vector2.up * (0.5f * Mathf.Max(0f, gravedad) * tiempo);
    }

    private bool Interrumpido() => cancelado || !isActiveAndEnabled ||
        jugador == null || vida == null || !vida.EstaVivo;

    private void CerrarBoca()
    {
        if (carga != null)
            Destroy(carga);
        carga = null;
        if (gusano != null)
            gusano.EstablecerBoca(false);
    }

    public void CancelarAtaque()
    {
        cancelado = true;
        CerrarBoca();
        foreach (ProyectilAcidoGusano proyectil in proyectiles)
            if (proyectil != null)
                Destroy(proyectil.gameObject);
        proyectiles.Clear();
    }
}
