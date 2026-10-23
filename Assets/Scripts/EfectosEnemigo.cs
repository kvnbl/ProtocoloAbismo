using UnityEngine;

public enum TipoMuerteEnemigo { Normal, Aplastamiento, Quemado }

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemigoBasico))]
public class EfectosEnemigo : MonoBehaviour
{
    [Header("Aspecto del enemigo")]
    public SpriteRenderer imagen;
    public Animator animador;

    [Header("Hojas completas, sin recortar")]
    public Texture2D muerteNormal;
    public Texture2D muerteAplastamiento;
    public Texture2D muerteCanon;
    public Texture2D impactoSangre;
    [Min(1f)] public float cuadrosPorSegundo = 16f;
    [Min(1f)] public float cuadrosSangrePorSegundo = 24f;

    [Header("Destello blanco")]
    public Shader shaderDestello;
    [Range(0f, 1f)] public float intensidadDestello = 0.35f;
    [Min(0.01f)] public float duracionDestello = 0.08f;

    [Header("Permanencia del cuerpo")]
    [Min(0f)] public float segundosCuerpoVisible = 2f;
    [Min(0.1f)] public float segundosParpadeo = 2f;
    [Min(0.01f)] public float intervaloLento = 0.4f;
    [Min(0.01f)] public float intervaloRapido = 0.045f;
    [Min(0.1f)] public float gravedadRestos = 1f;

    private Sprite[] normal, aplastamiento, quemado, secuencia;
    private SpriteRenderer capaBlanca;
    private Material materialBlanco;
    private Color colorOriginal;
    private Vector2 pivote;
    private float ppu;
    private float destelloRestante;
    private bool muerto;
    private float tiempoMuerte;
    private float siguienteParpadeo;
    private bool cuerpoVisible = true;
    private LayerMask capasSuelo;
    private Vector3 pieLocal;
    private float velocidadCaida;
    private readonly RaycastHit2D[] suelo = new RaycastHit2D[16];

    private void Awake()
    {
        if (imagen == null) imagen = GetComponentInChildren<SpriteRenderer>();
        if (animador == null) animador = GetComponentInChildren<Animator>();
        if (imagen == null || imagen.sprite == null)
        {
            Debug.LogError("EfectosEnemigo: asigna Imagen con su sprite original.", this);
            return;
        }
        colorOriginal = imagen.color;
        Sprite inicial = imagen.sprite;
        ppu = inicial.pixelsPerUnit;
        pivote = inicial.rect.size == new Vector2(256f, 256f)
            ? new Vector2(inicial.pivot.x / 256f, inicial.pivot.y / 256f)
            : Vector2.one * 0.5f;
        normal = CrearCuadros(muerteNormal, 3);
        aplastamiento = CrearCuadros(muerteAplastamiento, 2);
        quemado = CrearCuadros(muerteCanon, 4);

        if (shaderDestello == null)
            shaderDestello = Shader.Find("Juego/DestelloBlancoSprite");
        if (shaderDestello != null && shaderDestello.isSupported)
        {
            GameObject capa = new GameObject("DestelloBlanco");
            capa.layer = imagen.gameObject.layer;
            capa.transform.SetParent(imagen.transform, false);
            capaBlanca = capa.AddComponent<SpriteRenderer>();
            materialBlanco = new Material(shaderDestello);
            capaBlanca.sharedMaterial = materialBlanco;
            capaBlanca.enabled = false;
        }
        else
            Debug.LogWarning("EfectosEnemigo: falta un shader de destello compatible.", this);
    }

    private Sprite[] CrearCuadros(Texture2D textura, int filas)
    {
        if (textura == null || textura.width != 1024 || textura.height != filas * 256)
        {
            Debug.LogWarning("EfectosEnemigo: revisa la hoja de " + filas +
                " filas. Debe medir 1024x" + (filas * 256) + ".", this);
            return null;
        }
        Sprite[] resultado = new Sprite[4 * filas];
        for (int i = 0; i < resultado.Length; i++)
            resultado[i] = Sprite.Create(textura,
                new Rect(i % 4 * 256, (filas - 1 - i / 4) * 256, 256, 256),
                pivote, ppu, 0, SpriteMeshType.FullRect);
        return resultado;
    }

    public void Destellar()
    {
        destelloRestante = Mathf.Max(0.01f, duracionDestello);
    }

    public void MostrarSangre(Vector2 punto, Vector2 direccionSalida)
    {
        if (imagen != null && impactoSangre != null)
            ImpactoSangreEnemigo.Crear(impactoSangre, punto,
                direccionSalida, imagen, cuadrosSangrePorSegundo);
    }

    public void IniciarMuerte(TipoMuerteEnemigo tipo, LayerMask sueloEnemigo, float velocidadY)
    {
        if (muerto) return;
        if (imagen == null) { Destroy(gameObject); return; }
        muerto = true;
        tiempoMuerte = 0f;
        cuerpoVisible = true;
        capasSuelo = sueloEnemigo;
        velocidadCaida = Mathf.Min(0f, velocidadY);
        if (animador != null) animador.enabled = false;
        secuencia = tipo == TipoMuerteEnemigo.Aplastamiento ? aplastamiento
            : tipo == TipoMuerteEnemigo.Quemado ? quemado : normal;

        if (imagen != null)
        {
            // Todas las hojas conservan el apoyo del idle: x128, y208 desde arriba.
            Vector3 pieEnImagen = new Vector3(
                (128f - pivote.x * 256f) / Mathf.Max(0.01f, ppu),
                (48f - pivote.y * 256f) / Mathf.Max(0.01f, ppu), 0f);
            if (imagen.flipX) pieEnImagen.x = -pieEnImagen.x;
            if (imagen.flipY) pieEnImagen.y = -pieEnImagen.y;
            pieLocal = transform.InverseTransformPoint(imagen.transform.TransformPoint(pieEnImagen));
            imagen.enabled = true;
            if (secuencia != null) imagen.sprite = secuencia[0];
        }
    }

    private void Update()
    {
        if (!muerto) return;
        tiempoMuerte += Time.deltaTime;
        float fps = Mathf.Max(1f, cuadrosPorSegundo);
        float duracion = secuencia != null ? secuencia.Length / fps : 0f;
        if (imagen != null && secuencia != null)
            imagen.sprite = secuencia[Mathf.Min(secuencia.Length - 1,
                Mathf.FloorToInt(tiempoMuerte * fps))];

        float parpadeo = tiempoMuerte - duracion - Mathf.Max(0f, segundosCuerpoVisible);
        if (parpadeo < 0f || imagen == null) return;
        float progreso = Mathf.Clamp01(parpadeo / Mathf.Max(0.1f, segundosParpadeo));
        if (progreso >= 1f)
        {
            imagen.enabled = false;
            if (capaBlanca != null) capaBlanca.enabled = false;
            Destroy(gameObject);
            return;
        }
        if (parpadeo >= siguienteParpadeo)
        {
            cuerpoVisible = !cuerpoVisible;
            siguienteParpadeo = parpadeo + Mathf.Lerp(
                Mathf.Max(0.01f, intervaloLento), Mathf.Max(0.01f, intervaloRapido), progreso);
        }
        Color color = colorOriginal;
        color.a *= 1f - Mathf.InverseLerp(0.6f, 1f, progreso);
        imagen.color = color;
        imagen.enabled = cuerpoVisible;
    }

    private void FixedUpdate()
    {
        if (!muerto || imagen == null) return;
        velocidadCaida += Mathf.Min(-0.1f, Physics2D.gravity.y) *
            Mathf.Max(0.1f, gravedadRestos) * Time.fixedDeltaTime;
        float descenso = Mathf.Max(0f, -velocidadCaida * Time.fixedDeltaTime);
        Vector3 pie = transform.TransformPoint(pieLocal);
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasSuelo);
        filtro.useTriggers = false;
        int cantidad = Physics2D.Raycast((Vector2)pie + Vector2.up * 0.03f,
            Vector2.down, filtro, suelo, descenso + 0.03f);
        float altura = float.NegativeInfinity;
        for (int i = 0; i < cantidad; i++)
            if (suelo[i].collider != null && suelo[i].normal.y >= 0.65f)
                altura = Mathf.Max(altura, suelo[i].point.y);
        if (!float.IsNegativeInfinity(altura))
        {
            transform.position += Vector3.up * (altura - pie.y);
            velocidadCaida = 0f;
        }
        else transform.position += Vector3.down * descenso;
    }

    private void LateUpdate()
    {
        if (capaBlanca == null || imagen == null) return;
        destelloRestante = Mathf.Max(0f, destelloRestante - Time.deltaTime);
        capaBlanca.enabled = destelloRestante > 0f && imagen.enabled;
        if (!capaBlanca.enabled) return;
        capaBlanca.sprite = imagen.sprite;
        capaBlanca.flipX = imagen.flipX;
        capaBlanca.flipY = imagen.flipY;
        capaBlanca.sortingLayerID = imagen.sortingLayerID;
        capaBlanca.sortingOrder = imagen.sortingOrder + 1;
        capaBlanca.color = new Color(1f, 1f, 1f,
            Mathf.Clamp01(intensidadDestello) * imagen.color.a *
            destelloRestante / Mathf.Max(0.01f, duracionDestello));
    }

    private void OnDestroy()
    {
        DestruirCuadros(normal);
        DestruirCuadros(aplastamiento);
        DestruirCuadros(quemado);
        if (materialBlanco != null) Destroy(materialBlanco);
    }

    private void DestruirCuadros(Sprite[] cuadros)
    {
        if (cuadros == null) return;
        foreach (Sprite cuadro in cuadros)
            if (cuadro != null) Destroy(cuadro);
    }
}
