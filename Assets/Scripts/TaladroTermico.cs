using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TaladroTermico : MonoBehaviour
{
    [Header("Punta del taladro")]
    public Collider2D zonaDanio;
    public LayerMask capasEnemigos;

    [Header("Temperatura: solo contacto con enemigos")]
    [Min(0.01f)] public float segundosCalentamiento = 0.9f;
    [Min(0.01f)] public float segundosEnfriamiento = 1.5f;
    [SerializeField, Range(0f, 1f)] private float temperatura;

    [Header("Dano")]
    [Min(0)] public int danioMaximo = 1;
    [Min(0.01f)] public float intervaloDanio = 0.75f;
    [Min(0f)] public float retardoPrimerGolpe = 0.3f;

    [Header("Limite de uso: solo contacto con enemigos")]
    [Min(0.1f)] public float segundosUsoMaximo = 2f;
    [Min(0.1f)] public float segundosDescanso = 1.5f;
    [SerializeField] private float usoAcumulado;
    [SerializeField] private float descansoRestante;
    [SerializeField] private bool sobrecalentado;

    [Header("Hoja de animacion")]
    public SpriteRenderer imagen;
    [Tooltip("PNG completo de 2048x1536: 8 columnas, 6 filas, celdas de 256.")]
    public Texture2D hojaAnimacion;
    [Min(1f)] public float cuadrosPorSegundo = 16f;
    [Tooltip("PNG de idle de 2048x1280: 8 columnas y 5 filas termicas.")]
    public Texture2D hojaIdle;
    [Min(1f)] public float cuadrosPorSegundoIdle = 8f;
    [Tooltip("Solo se usa si el SpriteRenderer no tiene un sprite inicial.")]
    [Min(0.01f)] public float pixelesPorUnidad = 100f;
    [Tooltip("El Animator antiguo del taladro. Se desactiva mientras este script controla el sprite.")]
    public Animator animadorTaladro;

    private const int Columnas = 8;
    private const int Filas = 6;
    private const int Celda = 256;

    private readonly List<Collider2D> contactos = new List<Collider2D>(16);
    private readonly HashSet<EnemigoBasico> enemigos = new HashSet<EnemigoBasico>();
    private readonly Dictionary<VidaGusano, ZonaDanioGusano> gusanos =
        new Dictionary<VidaGusano, ZonaDanioGusano>();
    private Sprite[] cuadros;
    private Sprite[] cuadrosIdle;
    private Sprite spriteOriginal;
    private bool animadorEstabaActivo;
    private bool botonSolicitado;
    private bool esperarSoltar;
    private float tiempoContacto;
    private float proximoGolpe;
    private float faseGiro;
    private float faseIdle;

    public bool PuedeTaladrar =>
        botonSolicitado && isActiveAndEnabled &&
        !sobrecalentado && !esperarSoltar;

    public float Temperatura => temperatura;
    public bool Sobrecalentado => sobrecalentado;

    private void Awake()
    {
        if (imagen == null)
            imagen = GetComponent<SpriteRenderer>();

        if (imagen != null)
            spriteOriginal = imagen.sprite;

        if (animadorTaladro == null)
        {
            ExcavadoraController controlador =
                GetComponentInParent<ExcavadoraController>();

            if (controlador != null)
                animadorTaladro = controlador.animTaladro;

            if (animadorTaladro == null && imagen != null)
                animadorTaladro = imagen.GetComponent<Animator>();
        }

        PrepararCuadros();
    }

    private void OnEnable()
    {
        if (cuadros == null)
            return;

        if (animadorTaladro != null)
        {
            animadorEstabaActivo = animadorTaladro.enabled;
            animadorTaladro.enabled = false;
        }

        MostrarCuadro();
    }

    private void PrepararCuadros()
    {
        if (imagen == null || hojaAnimacion == null || hojaIdle == null)
        {
            Debug.LogError(
                "TaladroTermico: asigna Imagen, Hoja Animacion y Hoja Idle (PNG completos).", this);
            return;
        }

        if (hojaAnimacion.width != Columnas * Celda ||
            hojaAnimacion.height != Filas * Celda ||
            hojaIdle.width != Columnas * Celda ||
            hojaIdle.height != 5 * Celda)
        {
            Debug.LogError(
                "TaladroTermico: giro = 2048x1536 e idle = 2048x1280. " +
                "Revisa Max Size >= 2048 y Non Power of 2 = None si aparece.", this);
            return;
        }

        float ppu = spriteOriginal != null
            ? spriteOriginal.pixelsPerUnit : Mathf.Max(0.01f, pixelesPorUnidad);
        Vector2 pivote = Vector2.one * 0.5f;

        // Conserva el punto de enganche si el sprite anterior usa celdas de 256.
        if (spriteOriginal != null &&
            Mathf.Approximately(spriteOriginal.rect.width, Celda) &&
            Mathf.Approximately(spriteOriginal.rect.height, Celda))
        {
            pivote = new Vector2(
                spriteOriginal.pivot.x / Celda,
                spriteOriginal.pivot.y / Celda);
        }

        cuadros = CrearCuadros(hojaAnimacion, Filas, pivote, ppu);
        cuadrosIdle = CrearCuadros(hojaIdle, 5, pivote, ppu);
    }

    private Sprite[] CrearCuadros(
        Texture2D textura, int filas, Vector2 pivote, float ppu)
    {
        Sprite[] resultado = new Sprite[Columnas * filas];
        for (int fila = 0; fila < filas; fila++)
        {
            for (int columna = 0; columna < Columnas; columna++)
            {
                // El PNG se lee de arriba abajo; Rect usa origen inferior izquierdo.
                Rect rectangulo = new Rect(
                    columna * Celda, (filas - 1 - fila) * Celda, Celda, Celda);
                Sprite cuadro = Sprite.Create(
                    textura, rectangulo, pivote, ppu, 0, SpriteMeshType.FullRect);
                cuadro.name = textura.name + "_F" + fila + "_C" + columna;
                resultado[fila * Columnas + columna] = cuadro;
            }
        }
        return resultado;
    }

    public void EstablecerActivo(bool valor)
    {
        botonSolicitado = valor;
        if (!valor)
        {
            esperarSoltar = false;
            tiempoContacto = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (sobrecalentado)
        {
            ActualizarDescanso();
            return;
        }

        BuscarEnemigos();
        bool perforando = PuedeTaladrar && (enemigos.Count > 0 || gusanos.Count > 0);
        float paso = Time.fixedDeltaTime;
        float limite = Mathf.Max(0.1f, segundosUsoMaximo);

        if (perforando)
        {
            usoAcumulado = Mathf.Min(limite, usoAcumulado + paso);
            temperatura = Mathf.MoveTowards(
                temperatura, 1f, paso / Mathf.Max(0.01f, segundosCalentamiento));
        }
        else
        {
            // Puede seguir girando y romper bloques mientras baja la temperatura.
            temperatura = Mathf.MoveTowards(
                temperatura, 0f, paso / Mathf.Max(0.01f, segundosEnfriamiento));
            usoAcumulado = Mathf.MoveTowards(
                usoAcumulado, 0f,
                paso * limite / Mathf.Max(0.1f, segundosDescanso));
        }

        if (perforando && usoAcumulado >= limite)
        {
            sobrecalentado = true;
            esperarSoltar = true;
            descansoRestante = Mathf.Max(0.1f, segundosDescanso);
            tiempoContacto = 0f;
            faseGiro = 0f;
            return;
        }

        tiempoContacto = perforando ? tiempoContacto + paso : 0f;
        if (!perforando || tiempoContacto < Mathf.Max(0f, retardoPrimerGolpe))
            return;

        int porTemperatura = temperatura >= 0.999f
            ? 2 : temperatura >= 0.45f ? 1 : 0;
        int danio = Mathf.Min(porTemperatura, Mathf.Max(0, danioMaximo));
        if (danio == 0 || Time.time < proximoGolpe)
            return;

        proximoGolpe = Time.time + Mathf.Max(0.01f, intervaloDanio);

        List<EnemigoBasico> enemigosDelGolpe =
            new List<EnemigoBasico>(enemigos);

        List<ZonaDanioGusano> gusanosDelGolpe =
            new List<ZonaDanioGusano>(gusanos.Values);

        foreach (EnemigoBasico enemigo in enemigosDelGolpe)
        {
            if (!PuedeTaladrar)
                return;

            if (enemigo != null && enemigo.EstaVivo)
                enemigo.RecibirDanio(danio);
        }

        foreach (ZonaDanioGusano zona in gusanosDelGolpe)
        {
            if (!PuedeTaladrar)
                return;

            if (zona != null && zona.EstaActiva)
                zona.RecibirDanio(danio, TipoAtaqueGusano.Taladro);
        }
    }

    private void BuscarEnemigos()
    {
        enemigos.Clear();
        gusanos.Clear();
        contactos.Clear();

        if (!PuedeTaladrar || zonaDanio == null || !zonaDanio.enabled ||
            !zonaDanio.gameObject.activeInHierarchy)
            return;

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasEnemigos);
        filtro.useTriggers = true;
        Physics2D.OverlapCollider(zonaDanio, filtro, contactos);

        foreach (Collider2D contacto in contactos)
        {
            ZonaDanioGusano zona = contacto.GetComponent<ZonaDanioGusano>();
            if (zona != null && zona.EstaActiva)
            {
                ZonaDanioGusano anterior;
                if (!gusanos.TryGetValue(zona.Vida, out anterior) ||
                    zona.CalcularDanio(1, TipoAtaqueGusano.Taladro) >
                    anterior.CalcularDanio(1, TipoAtaqueGusano.Taladro))
                    gusanos[zona.Vida] = zona;
                continue;
            }
            if (contacto.isTrigger)
                continue;
            EnemigoBasico enemigo = contacto.GetComponentInParent<EnemigoBasico>();
            if (enemigo != null && enemigo.EstaVivo)
                enemigos.Add(enemigo);
        }
    }

    private void ActualizarDescanso()
    {
        float paso = Time.fixedDeltaTime;
        descansoRestante = Mathf.Max(0f, descansoRestante - paso);
        temperatura = Mathf.MoveTowards(
            temperatura, 0f, paso / Mathf.Max(0.1f, segundosDescanso));
        tiempoContacto = 0f;
        enemigos.Clear();
        gusanos.Clear();
        contactos.Clear();

        if (descansoRestante <= 0f)
        {
            sobrecalentado = false;
            usoAcumulado = 0f;
            temperatura = 0f;
            // Mantiene la regla anterior: soltar antes de volver a activar.
            esperarSoltar = botonSolicitado;
        }
    }

    private void LateUpdate()
    {
        if (cuadros == null || cuadrosIdle == null || imagen == null)
            return;

        if (PuedeTaladrar)
        {
            faseGiro = Mathf.Repeat(
                faseGiro + Time.deltaTime * Mathf.Max(1f, cuadrosPorSegundo),
                Columnas);
        }
        else if (!sobrecalentado)
        {
            faseIdle = Mathf.Repeat(
                faseIdle + Time.deltaTime * Mathf.Max(1f, cuadrosPorSegundoIdle),
                Columnas);
        }

        MostrarCuadro();
    }

    private void MostrarCuadro()
    {
        if (cuadros == null || cuadrosIdle == null || imagen == null)
            return;

        if (sobrecalentado)
        {
            float progreso = Mathf.Clamp01(
                1f - descansoRestante / Mathf.Max(0.1f, segundosDescanso));
            int columna = Mathf.Min(7, Mathf.FloorToInt(progreso * Columnas));
            imagen.sprite = cuadros[5 * Columnas + columna];
        }
        else
        {
            // Giro e idle comparten temperatura y conservan sus fases.
            int fila = temperatura <= 0.001f
                ? 0 : Mathf.Clamp(Mathf.CeilToInt(temperatura * 4f), 1, 4);
            float fase = PuedeTaladrar ? faseGiro : faseIdle;
            int columna = Mathf.Clamp(Mathf.FloorToInt(fase), 0, 7);
            Sprite[] hoja = PuedeTaladrar ? cuadros : cuadrosIdle;
            imagen.sprite = hoja[fila * Columnas + columna];
        }
    }

    private void OnDisable()
    {
        botonSolicitado = false;
        esperarSoltar = false;
        sobrecalentado = false;
        temperatura = 0f;
        usoAcumulado = 0f;
        descansoRestante = 0f;
        tiempoContacto = 0f;
        proximoGolpe = 0f;
        faseGiro = 0f;
        faseIdle = 0f;
        contactos.Clear();
        enemigos.Clear();
        gusanos.Clear();

        if (cuadros != null)
        {
            if (imagen != null)
                imagen.sprite = spriteOriginal;
            if (animadorTaladro != null)
                animadorTaladro.enabled = animadorEstabaActivo;
        }
    }

    private void OnDestroy()
    {
        DestruirCuadros(cuadros);
        DestruirCuadros(cuadrosIdle);
    }

    private void DestruirCuadros(Sprite[] sprites)
    {
        if (sprites == null)
            return;

        foreach (Sprite cuadro in sprites)
        {
            if (cuadro != null)
                Destroy(cuadro);
        }
    }
}
