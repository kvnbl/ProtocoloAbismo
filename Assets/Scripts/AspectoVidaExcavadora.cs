using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public class AspectoVidaExcavadora : MonoBehaviour
{
    public enum EstadoVisual { Sana, Danada, MuyDanada }

    [Header("Referencias")]
    public VidaJugador vida;
    public SpriteRenderer imagenChasis;
    public Texture2D hojaEstadosVida;

    [Header("Texturas originales que usa tu Animator")]
    public Texture2D idleOriginal;
    public Texture2D avanceOriginal;
    public Texture2D retrocesoOriginal;
    public Texture2D saltoOriginal;

    [Header("Limites de vida")]
    [Range(0f, 1f)] public float limiteDanada = 2f / 3f;
    [Range(0f, 1f)] public float limiteMuyDanada = 1f / 3f;

    [Header("Destello blanco")]
    public Shader shaderDestello;
    [Range(0f, 1f)] public float intensidadDestello = 0.35f;
    [Min(0.01f)] public float duracionDestello = 0.08f;

    [Header("Prueba visual durante Play")]
    public bool probarEstado;
    public EstadoVisual estadoDePrueba;

    private readonly Dictionary<Sprite, Sprite[]> variantes =
        new Dictionary<Sprite, Sprite[]>();
    private readonly Dictionary<Sprite, Sprite> originales =
        new Dictionary<Sprite, Sprite>();
    private SpriteRenderer capaBlanca;
    private Material materialBlanco;
    private float destelloRestante;
    private int ultimaVida = -1;
    private bool avisoSprite;

    private void Awake()
    {
        if (vida == null) vida = GetComponentInParent<VidaJugador>();
        if (imagenChasis == null || hojaEstadosVida == null ||
            hojaEstadosVida.width != 4096 || hojaEstadosVida.height != 2048)
        {
            Debug.LogError("AspectoVidaExcavadora: asigna Imagen Chasis y la hoja " +
                "completa4096x2048 con Max Size4096 o superior.", this);
            enabled = false;
            return;
        }
        if (vida == null)
            Debug.LogWarning("AspectoVidaExcavadora: asigna Vida para leer la salud.", this);

        if (shaderDestello == null)
            shaderDestello = Shader.Find("Juego/DestelloBlancoSprite");
        if (shaderDestello != null && shaderDestello.isSupported)
        {
            GameObject capa = new GameObject("DestelloChasis");
            capa.layer = imagenChasis.gameObject.layer;
            capa.transform.SetParent(imagenChasis.transform, false);
            capaBlanca = capa.AddComponent<SpriteRenderer>();
            materialBlanco = new Material(shaderDestello);
            capaBlanca.sharedMaterial = materialBlanco;
            capaBlanca.enabled = false;
        }
        else Debug.LogWarning("AspectoVidaExcavadora: asigna Shader Destello " +
            "para mostrar el golpe blanco.", this);
    }

    private void OnEnable()
    {
        ultimaVida = -1;
        destelloRestante = 0f;
    }

    private void LateUpdate()
    {
        destelloRestante = Mathf.Max(0f, destelloRestante - Time.deltaTime);
        if (vida != null)
        {
            if (ultimaVida >= 0 && vida.VidaActual < ultimaVida)
                destelloRestante = Mathf.Max(0.01f, duracionDestello);
            ultimaVida = vida.VidaActual;
        }

        if (imagenChasis == null) return;
        Sprite fuente = imagenChasis.sprite;
        if (fuente != null)
        {
            // Si el Animator no escribio otro cuadro, recupera su fuente original.
            if (originales.TryGetValue(fuente, out Sprite original))
                fuente = original;
            if (!variantes.TryGetValue(fuente, out Sprite[] conjunto))
                conjunto = CrearVariantes(fuente);
            if (conjunto != null)
                imagenChasis.sprite = conjunto[ObtenerEstado()];
        }
        ActualizarDestello();
    }

    private int ObtenerEstado()
    {
        if (probarEstado) return (int)estadoDePrueba;
        if (vida == null) return 0;
        float porcentaje = (float)vida.VidaActual / Mathf.Max(1, vida.vidaMaxima);
        float critico = Mathf.Min(limiteDanada, limiteMuyDanada);
        float medio = Mathf.Max(limiteDanada, limiteMuyDanada);
        return porcentaje <= critico ? 2 : porcentaje <= medio ? 1 : 0;
    }

    private Sprite[] CrearVariantes(Sprite fuente)
    {
        Texture2D textura = fuente.texture;
        bool idle = textura == idleOriginal;
        bool avance = textura == avanceOriginal;
        bool retroceso = textura == retrocesoOriginal;
        bool salto = textura == saltoOriginal;
        Rect rect = fuente.rect;
        int columnas = idle ? 2 : 4;
        int filas = idle ? 3 : 2;

        if ((!idle && !avance && !retroceso && !salto) ||
            textura.width != columnas * 256 || textura.height != filas * 256 ||
            rect.width > 256 || rect.height > 256)
        {
            if (!avisoSprite)
            {
                avisoSprite = true;
                Debug.LogWarning("AspectoVidaExcavadora: el cuadro " + fuente.name +
                    " no coincide con las cuatro texturas originales. Asigna los PNG " +
                    "que realmente usan tus clips y conserva los sprites originales.", this);
            }
            variantes[fuente] = null;
            return null;
        }

        int columnaOriginal = Mathf.FloorToInt(rect.center.x / 256f);
        int filaInferior = Mathf.FloorToInt(rect.center.y / 256f);
        int indice = (filas - 1 - filaInferior) * columnas + columnaOriginal;
        int columnaAtlas = idle ? indice : avance ? 6 + indice
            : retroceso ? indice : 8 + indice;
        int filaBase = idle || avance ? 0 : 1;

        // Conserva el origen del sprite incluso si su recorte original era ajustado.
        Vector2 inicioCelda = new Vector2(columnaOriginal * 256, filaInferior * 256);
        Vector2 pivote = (rect.position + fuente.pivot - inicioCelda) / 256f;
        Sprite[] conjunto = new Sprite[3];
        conjunto[0] = fuente;
        for (int nivel = 1; nivel < 3; nivel++)
        {
            int fila = filaBase + nivel * 2;
            conjunto[nivel] = Sprite.Create(hojaEstadosVida,
                new Rect(columnaAtlas * 256, 2048 - (fila + 1) * 256, 256, 256),
                pivote, fuente.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            conjunto[nivel].name = fuente.name + "_Vida_" + nivel;
            originales[conjunto[nivel]] = fuente;
        }
        variantes[fuente] = conjunto;
        return conjunto;
    }

    private void ActualizarDestello()
    {
        if (capaBlanca == null) return;
        capaBlanca.enabled = destelloRestante > 0f && imagenChasis.enabled &&
            imagenChasis.sprite != null;
        if (!capaBlanca.enabled) return;
        capaBlanca.sprite = imagenChasis.sprite;
        capaBlanca.flipX = imagenChasis.flipX;
        capaBlanca.flipY = imagenChasis.flipY;
        capaBlanca.sortingLayerID = imagenChasis.sortingLayerID;
        capaBlanca.sortingOrder = imagenChasis.sortingOrder + 1;
        capaBlanca.color = new Color(1f, 1f, 1f,
            Mathf.Clamp01(intensidadDestello) * imagenChasis.color.a *
            destelloRestante / Mathf.Max(0.01f, duracionDestello));
    }

    private void OnDisable()
    {
        if (imagenChasis != null && imagenChasis.sprite != null &&
            originales.TryGetValue(imagenChasis.sprite, out Sprite fuente))
            imagenChasis.sprite = fuente;
        if (capaBlanca != null) capaBlanca.enabled = false;
    }

    private void OnDestroy()
    {
        foreach (Sprite[] conjunto in variantes.Values)
        {
            if (conjunto == null) continue;
            for (int i = 1; i < conjunto.Length; i++)
                if (conjunto[i] != null) Destroy(conjunto[i]);
        }
        if (capaBlanca != null) Destroy(capaBlanca.gameObject);
        if (materialBlanco != null) Destroy(materialBlanco);
    }
}
