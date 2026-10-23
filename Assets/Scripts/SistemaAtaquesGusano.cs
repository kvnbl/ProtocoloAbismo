using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GusanoModularPrueba))]
public class SistemaAtaquesGusano : MonoBehaviour
{
    [Header("Solo las entradas de la sala, no un recorrido")]
    public Transform madrigueraSuelo;
    public Transform paredIzquierda;
    public Transform paredDerecha;
    public ControlCamara controlCamara;

    [Header("Ritmo")]
    [Min(0.2f)] public float aviso = 0.8f;
    [Min(0.3f)] public float exposicion = 1.6f;
    [Min(0.5f)] public float descanso = 2.5f;
    [Tooltip("Velocidad medida en diametros del cuerpo por segundo.")]
    [Min(0.2f)] public float velocidadRelativa = 1.7f;
    [Tooltip("Cuanto cuerpo sobresale al asomarse, en diametros.")]
    [Range(0.5f, 2f)] public float alturaAsomarse = 0.9f;
    [Range(0.5f, 2f)] public float curvaPasada = 1.1f;

    public enum Estado { Inactivo, Avisando, Emergiendo, Expuesto, Retirandose, Pasada, Descansando }
    [SerializeField] private Estado estado;
    public Estado EstadoActual => estado;
    public bool EnCombate { get; private set; }

    private GusanoModularPrueba gusano;
    private VidaGusano vida;
    private AtaqueRocasGusano rocas;
    private AtaqueAcidoGusano acido;
    private EfectoExcavarGusano efectoExcavar;
    private Transform ultimaPared;
    private int ciclo;
    private GameObject polvo;

    private void Awake()
    {
        gusano = GetComponent<GusanoModularPrueba>();
        vida = GetComponent<VidaGusano>();
        rocas = GetComponent<AtaqueRocasGusano>();
        acido = GetComponent<AtaqueAcidoGusano>();
        efectoExcavar = GetComponent<EfectoExcavarGusano>();
    }

    public void IniciarCombate()
    {
        if (EnCombate || !isActiveAndEnabled || !gusano.Preparado)
            return;
        if (madrigueraSuelo == null || paredIzquierda == null || paredDerecha == null)
        {
            Debug.LogError("SistemaAtaquesGusano: asigna Madriguera Suelo, Pared Izquierda " +
                "y Pared Derecha. Los antiguos Puntos Recorrido ya no se usan.", this);
            return;
        }
        if (controlCamara == null && Camera.main != null)
            controlCamara = Camera.main.GetComponent<ControlCamara>();
        EnCombate = true;
        ciclo = 0;
        ultimaPared = Random.value < 0.5f ? paredIzquierda : paredDerecha;
        StartCoroutine(Combate());
    }

    private IEnumerator Combate()
    {
        while (EnCombate && vida != null && vida.EstaVivo)
        {
            if (ciclo % 3 == 0)
                yield return Asomarse(madrigueraSuelo, Vector2.up);
            else if (ciclo % 3 == 1)
            {
                // Alterna lados entre ciclos completos para que no repita siempre una pared.
                ultimaPared = ultimaPared == paredDerecha ? paredIzquierda : paredDerecha;
                yield return PasadaBaja(ultimaPared);
            }
            else
            {
                Vector2 haciaSala = ultimaPared == paredDerecha ? Vector2.left : Vector2.right;
                yield return Asomarse(ultimaPared, haciaSala);
            }
            ciclo++;
            estado = Estado.Descansando;
            float tiempo = 0f;
            // No encadena otra salida mientras siguen cayendo las rocas anteriores.
            while (tiempo < descanso || (rocas != null && rocas.AtaqueEnCurso))
            {
                if (!gusano.pausar)
                    tiempo += Time.deltaTime;
                yield return null;
            }
        }
        EnCombate = false;
        estado = Estado.Inactivo;
    }

    private IEnumerator Avisar(Transform entrada)
    {
        estado = Estado.Avisando;
        if (controlCamara != null)
            controlCamara.Sacudir(0.25f, 0.045f);
        if (rocas != null && rocas.prefabRoca != null)
            polvo = EfectoRocaJefe.Crear(rocas.hojaEfectos, 1, entrada.position,
                gusano.DiametroCuerpo * 1.3f, rocas.prefabRoca.Imagen,
                Mathf.Max(0.2f, aviso) + 0.1f, true);
        yield return Esperar(Mathf.Max(0.2f, aviso));
        if (polvo != null)
            Destroy(polvo);
        polvo = null;
    }

    private IEnumerator Asomarse(Transform entrada, Vector2 normal)
    {
        yield return Avisar(entrada);
        float diametro = gusano.DiametroCuerpo;
        Vector2 superficie = entrada.position;
        Vector2 principio = superficie - normal * diametro * 1.5f;
        // El morro ya se extiende desde la union cabeza/cuello: no sacamos todo el gusano.
        Vector2 final = superficie + normal * diametro * Mathf.Max(0.5f, alturaAsomarse);
        List<Vector2> muestras = new List<Vector2>();
        AgregarLinea(muestras, principio, final, 60);
        gusano.ConfigurarRutaAtaque(muestras);
        estado = Estado.Emergiendo;
        yield return Mover(0f, gusano.LongitudRuta, 1f);
        estado = Estado.Expuesto;
        // El suelo conserva su ataque de rocas; el acido se escupe desde las paredes.
        if (Mathf.Abs(normal.x) > 0.5f && acido != null && acido.isActiveAndEnabled)
            yield return acido.Escupir();
        else
            yield return Esperar(Mathf.Max(0.3f, exposicion));
        estado = Estado.Retirandose;
        if (efectoExcavar != null)
            efectoExcavar.Comenzar(superficie, normal,
                gusano.LongitudRuta / Mathf.Max(0.01f, diametro * velocidadRelativa * 0.85f));
        yield return Mover(gusano.LongitudRuta, 0f, 0.85f);
        gusano.OcultarAtaque();
        gusano.AvisarEntierro(superficie, -normal);
    }

    private IEnumerator PasadaBaja(Transform salida)
    {
        yield return Avisar(madrigueraSuelo);
        float diametro = gusano.DiametroCuerpo;
        Vector2 entrada = madrigueraSuelo.position;
        Vector2 fin = salida.position;
        Vector2 haciaFuera = salida == paredDerecha ? Vector2.right : Vector2.left;
        float control = Mathf.Min(Vector2.Distance(entrada, fin) * 0.55f,
            diametro * Mathf.Max(0.5f, curvaPasada));
        Vector2 c1 = entrada + Vector2.up * control;
        Vector2 c2 = fin - haciaFuera * control;
        List<Vector2> muestras = new List<Vector2>();
        AgregarLinea(muestras, entrada - Vector2.up * diametro * 1.5f, entrada, 25);
        for (int i = 1; i <= 100; i++)
        {
            float t = i / 100f;
            float s = 1f - t;
            muestras.Add(s * s * s * entrada + 3f * s * s * t * c1 +
                3f * s * t * t * c2 + t * t * t * fin);
        }
        float distanciaPared = Medir(muestras);
        AgregarLinea(muestras, fin, fin + haciaFuera * (gusano.LongitudVisual + diametro * 2f), 100);
        gusano.ConfigurarRutaAtaque(muestras);
        estado = Estado.Pasada;
        float distancia = 0f;
        bool avisado = false;
        bool polvoIniciado = false;
        while (distancia < gusano.LongitudRuta)
        {
            if (!gusano.pausar)
            {
                float progreso = distancia / Mathf.Max(0.01f, distanciaPared);
                float aceleracion = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(progreso * 3f));
                distancia += diametro * Mathf.Max(0.2f, velocidadRelativa) * aceleracion * Time.deltaTime;
                gusano.ColocarCabezaEnRuta(distancia);
                if (!polvoIniciado && distancia >= distanciaPared - diametro * 0.4f)
                {
                    polvoIniciado = true;
                    if (efectoExcavar != null)
                        efectoExcavar.Comenzar(fin, -haciaFuera,
                            (gusano.LongitudRuta - distancia) / Mathf.Max(0.01f, diametro * velocidadRelativa));
                }
                if (!avisado && distancia >= distanciaPared + diametro)
                {
                    avisado = true;
                    gusano.AvisarEntierro(fin, haciaFuera);
                }
            }
            yield return null;
        }
        gusano.OcultarAtaque();
    }

    private IEnumerator Mover(float desde, float hasta, float multiplicador)
    {
        float distancia = Mathf.Abs(hasta - desde);
        float segundos = distancia / Mathf.Max(0.01f,
            gusano.DiametroCuerpo * velocidadRelativa * multiplicador);
        float tiempo = 0f;
        while (tiempo < segundos)
        {
            if (!gusano.pausar)
                tiempo += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tiempo / Mathf.Max(0.01f, segundos)));
            gusano.ColocarCabezaEnRuta(Mathf.Lerp(desde, hasta, t));
            yield return null;
        }
        gusano.ColocarCabezaEnRuta(hasta);
    }

    private IEnumerator Esperar(float segundos)
    {
        float tiempo = 0f;
        while (tiempo < segundos)
        {
            if (!gusano.pausar)
                tiempo += Time.deltaTime;
            yield return null;
        }
    }

    private static void AgregarLinea(List<Vector2> lista, Vector2 a, Vector2 b, int pasos)
    {
        if (lista.Count == 0)
            lista.Add(a);
        for (int i = 1; i <= pasos; i++)
            lista.Add(Vector2.Lerp(a, b, i / (float)pasos));
    }

    private static float Medir(List<Vector2> lista)
    {
        float suma = 0f;
        for (int i = 1; i < lista.Count; i++)
            suma += Vector2.Distance(lista[i - 1], lista[i]);
        return suma;
    }

    public void DetenerCombate()
    {
        StopAllCoroutines();
        EnCombate = false;
        estado = Estado.Inactivo;
        if (polvo != null)
            Destroy(polvo);
        polvo = null;
        if (gusano != null)
            gusano.OcultarAtaque();
        if (rocas != null)
            rocas.CancelarAtaque();
        if (acido != null)
            acido.CancelarAtaque();
        if (efectoExcavar != null)
            efectoExcavar.Limpiar();
    }

    private void OnDisable() => DetenerCombate();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (madrigueraSuelo != null)
            Gizmos.DrawWireSphere(madrigueraSuelo.position, 0.3f);
        if (paredIzquierda != null)
            Gizmos.DrawWireSphere(paredIzquierda.position, 0.3f);
        if (paredDerecha != null)
            Gizmos.DrawWireSphere(paredDerecha.position, 0.3f);
    }
}
