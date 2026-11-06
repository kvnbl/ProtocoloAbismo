using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Responsabilidad: Coordina los estados y la secuencia de ataques del jefe.
[DisallowMultipleComponent]
[RequireComponent(typeof(GusanoModularPrueba))]
public class SistemaAtaquesGusano : MonoBehaviour
{

    #region Datos, configuracion y estado

    [Header("Solo las entradas de la sala, no un recorrido")]
    public Transform madrigueraSuelo;

    [Tooltip("Salida opcional. Coloca el punto sobre la superficie del piso, dentro de la sala.")]
    public Transform salidaSueloIzquierda;

    [Tooltip("Salida opcional. Coloca el punto sobre la superficie del piso, dentro de la sala.")]
    public Transform salidaSueloDerecha;

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

    [Header("Mirada al salir del suelo")]
    [Tooltip("Asigna la excavadora o un punto en su chasis. Si lo dejas vacío, se busca al jugador.")]
    [SerializeField] private Transform objetivoMirada;

    [Header("Mordisco desde el suelo")]
    [Min(0.3f)] public float avisoMordisco = 0.7f;

    [Min(0.2f)] public float recuperacionMordisco = 0.65f;

    [Tooltip("Radio de golpe como fracción del diámetro del gusano.")]
    [Range(0.1f, 0.65f)] public float radioRelativoMordisco = 0.38f;

    [Min(1)] public int danioMordisco = 1;

    public enum Estado
    {
        Inactivo, Avisando, Emergiendo, Expuesto, Retirandose, Pasada, Descansando,
        AvisandoMordisco, Mordiendo, RecuperandoMordisco
    }

    [SerializeField] private Estado estado;

    public Estado EstadoActual => estado;

    public bool EnCombate { get; private set; }

    private GusanoModularPrueba gusano;

    private VidaGusano vida;

    private AtaqueRocasGusano rocas;

    private AtaqueAcidoGusano acido;

    private EfectoExcavarGusano efectoExcavar;

    private Transform ultimaPared;

    private Transform ultimaSalidaSuelo;

    private int ciclo;

    private Transform objetivoMiradaActual;

    private float inclinacionMirada;

    private float anguloRutaSuelo;

    private float ladoSalidaSuelo;

    private GameObject polvo;

    private GameObject marcaMordisco;

    #endregion

    #region Inicializacion y activacion

    private void Awake()
    {
        gusano = GetComponent<GusanoModularPrueba>();
        vida = GetComponent<VidaGusano>();
        rocas = GetComponent<AtaqueRocasGusano>();
        acido = GetComponent<AtaqueAcidoGusano>();
        efectoExcavar = GetComponent<EfectoExcavarGusano>();
    }

    #endregion

    #region Acciones disponibles para otros componentes

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
        ultimaSalidaSuelo = null;
        ultimaPared = Random.value < 0.5f ? paredIzquierda : paredDerecha;
        StartCoroutine(Combate());
    }

    public void DetenerCombate()
    {
        StopAllCoroutines();
        EnCombate = false;
        estado = Estado.Inactivo;
        if (polvo != null)
            Destroy(polvo);
        polvo = null;
        QuitarMarcaMordisco();
        if (gusano != null)
            gusano.OcultarAtaque();
        if (rocas != null)
            rocas.CancelarAtaque();
        if (acido != null)
            acido.CancelarAtaque();
        if (efectoExcavar != null)
            efectoExcavar.Limpiar();
    }

    #endregion

    #region Logica interna

    private IEnumerator Combate()
    {
        while (EnCombate && vida != null && vida.EstaVivo)
        {
            if (ciclo % 3 == 0)
                yield return AsomarseDesdeSuelo();
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

    private IEnumerator AsomarseDesdeSuelo()
    {
        objetivoMiradaActual = ResolverObjetivoMirada();
        Transform entrada = ElegirSalidaSuelo();
        ultimaSalidaSuelo = entrada;
        float diametro = gusano.DiametroCuerpo;
        Vector2 superficie = entrada.position;
        Vector2 objetivo = objetivoMiradaActual != null
            ? (Vector2)objetivoMiradaActual.position
            : superficie + Vector2.right * diametro * 2f;

        // La entrada y el lado quedan fijos desde que aparece el aviso.
        float diferenciaX = objetivo.x - superficie.x;
        ladoSalidaSuelo = Mathf.Abs(diferenciaX) > 0.05f
            ? Mathf.Sign(diferenciaX)
            : (ultimaPared == paredDerecha ? -1f : 1f);
        Vector2 haciaLado = Vector2.right * ladoSalidaSuelo;
        Vector2 final = superficie + haciaLado * (diametro * 0.66f) +
            Vector2.up * (diametro * Mathf.Max(0.5f, alturaAsomarse));
        Vector2 haciaJugador = objetivo - final;
        float elevacion = Mathf.Clamp(Mathf.Atan2(haciaJugador.y,
            Mathf.Max(diametro * 0.7f, Mathf.Abs(haciaJugador.x))) * Mathf.Rad2Deg,
            -18f, 20f) * Mathf.Deg2Rad;
        Vector2 direccionFinal = haciaLado * Mathf.Cos(elevacion) + Vector2.up * Mathf.Sin(elevacion);
        Vector2 inicio = superficie - Vector2.up * (diametro * 1.5f) - haciaLado * (diametro * 0.55f);
        Vector2 control1 = superficie + Vector2.up * (diametro * 0.2f) - haciaLado * (diametro * 0.48f);
        Vector2 control2 = final - direccionFinal * (diametro * 0.58f);
        List<Vector2> muestras = new List<Vector2>(81) { inicio };
        AgregarCurva(muestras, inicio, control1, control2, final, 80);
        anguloRutaSuelo = Vector2.SignedAngle(Vector2.up, direccionFinal);

        yield return Avisar(entrada);
        gusano.EstablecerBoca(false);
        gusano.ConfigurarRutaAtaque(muestras, reflejar: ladoSalidaSuelo > 0f);
        estado = Estado.Emergiendo;
        yield return Mover(0f, gusano.LongitudRuta, 1f);
        estado = Estado.Expuesto;
        yield return Esperar(Mathf.Max(0.3f, exposicion));
        yield return MorderDesdeSuelo();
        yield return Retirarse(superficie, Vector2.up);
    }

    private Transform ElegirSalidaSuelo()
    {
        Transform mejor = madrigueraSuelo;
        if (objetivoMiradaActual == null)
            return mejor;

        float x = objetivoMiradaActual.position.x;
        float diametro = gusano.DiametroCuerpo;
        float mejorPuntaje = PuntuarSalida(mejor, x, diametro);
        PuntuarAlternativa(salidaSueloIzquierda, x, diametro, ref mejor, ref mejorPuntaje);
        PuntuarAlternativa(salidaSueloDerecha, x, diametro, ref mejor, ref mejorPuntaje);
        return mejor;
    }

    private void PuntuarAlternativa(Transform alternativa, float x, float diametro,
        ref Transform mejor, ref float mejorPuntaje)
    {
        if (alternativa == null || alternativa == mejor)
            return;

        Camera camara = controlCamara != null ? controlCamara.CamaraJuego : null;
        if (camara != null)
        {
            Vector3 punto = camara.WorldToViewportPoint(alternativa.position);
            if (punto.z <= 0f || punto.x < 0.06f || punto.x > 0.94f ||
                punto.y < 0.02f || punto.y > 0.88f)
                return;
        }

        float puntaje = PuntuarSalida(alternativa, x, diametro);
        if (puntaje < mejorPuntaje)
        {
            mejor = alternativa;
            mejorPuntaje = puntaje;
        }
    }

    private float PuntuarSalida(Transform entrada, float x, float diametro)
    {
        float distancia = Mathf.Abs(entrada.position.x - x);
        // Prefiere asomarse a un lado, a alcance de mordisco, no debajo del chasis.
        return Mathf.Abs(distancia - diametro * 1.7f) +
            (distancia < diametro * 0.65f ? diametro : 0f) +
            (entrada == ultimaSalidaSuelo ? diametro * 0.4f : 0f);
    }

    private IEnumerator Asomarse(Transform entrada, Vector2 normal)
    {
        yield return Avisar(entrada);
        float diametro = gusano.DiametroCuerpo;
        Vector2 superficie = entrada.position;
        Vector2 principio = superficie - normal * diametro * 1.5f;
        Vector2 final = superficie + normal * diametro * Mathf.Max(0.5f, alturaAsomarse);
        List<Vector2> muestras = new List<Vector2>();
        AgregarLinea(muestras, principio, final, 60);
        gusano.ConfigurarRutaAtaque(muestras);
        estado = Estado.Emergiendo;
        yield return Mover(0f, gusano.LongitudRuta, 1f);
        estado = Estado.Expuesto;
        if (acido != null && acido.isActiveAndEnabled)
            yield return acido.Escupir();
        else
            yield return Esperar(Mathf.Max(0.3f, exposicion));
        yield return Retirarse(superficie, normal);
    }

    private IEnumerator Retirarse(Vector2 superficie, Vector2 normal)
    {
        estado = Estado.Retirandose;
        if (efectoExcavar != null)
            efectoExcavar.Comenzar(superficie, normal, gusano.LongitudRuta /
                Mathf.Max(0.01f, gusano.DiametroCuerpo * velocidadRelativa * 0.85f));
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
        AgregarCurva(muestras, entrada, c1, c2, fin, 100);
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

    private IEnumerator MorderDesdeSuelo()
    {
        if (objetivoMiradaActual == null)
            yield break;

        // Solo inclina un poco la cabeza para morder. No gira al lado contrario
        // aunque la excavadora cambie de lado después del aviso de salida.
        float anguloInicial = anguloRutaSuelo;
        float anguloFijo = Mathf.Clamp(anguloInicial - ladoSalidaSuelo * 12f, -115f, 115f);
        estado = Estado.AvisandoMordisco;
        gusano.EstablecerBoca(true);
        inclinacionMirada = anguloFijo;
        AplicarInclinacion();
        bool hayMordisco = gusano.ObtenerBoca(out Vector2 centro, out _);
        inclinacionMirada = anguloInicial;
        AplicarInclinacion();

        float avisoReal = Mathf.Max(0.3f, avisoMordisco);
        float radio = gusano.DiametroCuerpo * Mathf.Clamp(radioRelativoMordisco, 0.1f, 0.65f);
        if (hayMordisco && rocas != null && rocas.prefabRoca != null)
        {
            marcaMordisco = EfectoRocaJefe.Crear(rocas.hojaEfectos, 1,
                new Vector3(centro.x, centro.y, transform.position.z), radio * 2f,
                rocas.prefabRoca.Imagen, avisoReal + 0.2f, true);
            if (marcaMordisco != null)
                UtilidadAtaquesJefe.Ordenar(marcaMordisco.GetComponent<SpriteRenderer>(),
                    rocas.sortingLayerRocas, rocas.ordenRocas + 3);
        }

        if (controlCamara != null)
            controlCamara.Sacudir(0.16f, 0.035f);

        float tiempoCargar = Mathf.Min(0.24f, avisoReal * 0.5f);
        yield return GirarParaMordisco(anguloInicial + ladoSalidaSuelo * 8f, tiempoCargar);
        yield return Esperar(avisoReal - tiempoCargar);
        estado = Estado.Mordiendo;
        yield return GirarParaMordisco(anguloFijo, 0.13f);

        // Se comprueba un solo golpe al cerrar las fauces, en el punto anunciado.
        QuitarMarcaMordisco();
        gusano.EstablecerBoca(false);
        AplicarInclinacion();
        if (hayMordisco)
            GolpearConMordisco(centro, radio);
        if (controlCamara != null)
            controlCamara.Sacudir(0.18f, 0.06f);

        estado = Estado.RecuperandoMordisco;
        yield return Esperar(Mathf.Max(0.2f, recuperacionMordisco));
        yield return GirarParaMordisco(anguloRutaSuelo, 0.14f);
    }

    private IEnumerator GirarParaMordisco(float destino, float duracion)
    {
        float inicio = inclinacionMirada;
        float tiempo = 0f;
        while (tiempo < duracion)
        {
            if (!gusano.pausar)
                tiempo += Time.deltaTime;
            inclinacionMirada = Mathf.SmoothStep(inicio, destino,
                Mathf.Clamp01(tiempo / duracion));
            AplicarInclinacion();
            yield return null;
        }
    }

    private void GolpearConMordisco(Vector2 centro, float radio)
    {
        VidaJugador jugador = objetivoMiradaActual != null
            ? objetivoMiradaActual.GetComponentInParent<VidaJugador>() : null;
        if (jugador == null)
            jugador = FindFirstObjectByType<VidaJugador>();
        if (jugador == null || !jugador.isActiveAndEnabled)
            return;

        ExcavadoraController excavadora = jugador.GetComponentInParent<ExcavadoraController>();
        Transform raiz = excavadora != null ? excavadora.transform : jugador.transform;
        foreach (Collider2D cuerpo in raiz.GetComponentsInChildren<Collider2D>())
        {
            if (!cuerpo.enabled || cuerpo.isTrigger)
                continue;
            if ((cuerpo.ClosestPoint(centro) - centro).sqrMagnitude > radio * radio)
                continue;
            jugador.RecibirDanio(Mathf.Max(1, danioMordisco));
            return;
        }
    }

    private void QuitarMarcaMordisco()
    {
        if (marcaMordisco != null)
            Destroy(marcaMordisco);
        marcaMordisco = null;
    }

    private Transform ResolverObjetivoMirada()
    {
        if (objetivoMirada != null)
            return objetivoMirada;

        VidaJugador jugador = rocas != null ? rocas.jugador : null;
        if (jugador == null && acido != null)
            jugador = acido.jugador;
        if (jugador == null && controlCamara != null && controlCamara.objetivo != null)
            jugador = controlCamara.objetivo.GetComponentInParent<VidaJugador>();
        if (jugador == null)
            jugador = FindFirstObjectByType<VidaJugador>();
        return jugador != null ? jugador.transform : null;
    }

    private void AplicarInclinacion()
    {
        Vector2 direccion = Quaternion.Euler(0f, 0f, inclinacionMirada) * Vector3.up;
        gusano.OrientarCabeza(direccion);
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

    private static void AgregarCurva(List<Vector2> lista, Vector2 inicio, Vector2 c1,
        Vector2 c2, Vector2 fin, int pasos)
    {
        for (int i = 1; i <= pasos; i++)
        {
            float t = i / (float)pasos;
            float s = 1f - t;
            lista.Add(s * s * s * inicio + 3f * s * s * t * c1 +
                3f * s * t * t * c2 + t * t * t * fin);
        }
    }

    private static float Medir(List<Vector2> lista)
    {
        float suma = 0f;
        for (int i = 1; i < lista.Count; i++)
            suma += Vector2.Distance(lista[i - 1], lista[i]);
        return suma;
    }

    #endregion

    #region Desactivacion y limpieza

    private void OnDisable() => DetenerCombate();

    #endregion

    #region Ayudas visuales del editor

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (madrigueraSuelo != null)
            Gizmos.DrawWireSphere(madrigueraSuelo.position, 0.3f);
        if (salidaSueloIzquierda != null)
            Gizmos.DrawWireSphere(salidaSueloIzquierda.position, 0.3f);
        if (salidaSueloDerecha != null)
            Gizmos.DrawWireSphere(salidaSueloDerecha.position, 0.3f);
        if (paredIzquierda != null)
            Gizmos.DrawWireSphere(paredIzquierda.position, 0.3f);
        if (paredDerecha != null)
            Gizmos.DrawWireSphere(paredDerecha.position, 0.3f);
    }

    #endregion

}
