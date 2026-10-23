using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HUDMunicionCanonSimple : MonoBehaviour
{
    public ExcavadoraController jugador;
    [Tooltip("El HUD decide su visibilidad leyendo EnSecuencia del jugador.")]
    public bool ocultarDuranteSecuencias = true;
    [Header("Aspecto")]
    [Tooltip("Opcional: si queda vacio, carga Resources/HUDCanon/PanelCanon.png.")]
    public Sprite fondo;
    [Tooltip("0,1 = arriba izquierda; 0,0 = abajo izquierda; 1,1 = arriba derecha.")]
    public Vector2 ancla = new Vector2(0f, 1f);
    public Vector2 margen = new Vector2(28f, 28f);
    [Min(0.25f)] public float escala = 1f;
    public Color colorCargado = new Color(1f, 0.88f, 0.55f);
    public Color colorVacio = new Color(1f, 0.25f, 0.16f);

    private Canvas canvas;
    private RectTransform panel;
    private Image imagenPanel;
    private Sprite spriteCreado;
    private readonly List<Image> pixeles = new List<Image>();
    private bool visible = true;
    private int ultimaMunicion = -1;
    private int ultimaCapacidad = -1;
    private Color ultimoColor;

    // Digitos 3x5: el contador siempre se dibuja con pixeles rectos, sin fuentes.
    private static readonly string[] digitos = {
        "111101101101111", "010110010010111", "111001111100111",
        "111001111001111", "101101111001001", "111100111001111",
        "111100111101111", "111001001001001", "111101111101111",
        "111101111001111"
    };
    private const string barra = "001001010100100";

    private void Start()
    {
        if (jugador == null)
            jugador = FindFirstObjectByType<ExcavadoraController>();
        if (fondo == null)
            fondo = Resources.Load<Sprite>("HUDCanon/PanelCanon");
        if (fondo == null)
        {
            Texture2D textura = Resources.Load<Texture2D>("HUDCanon/PanelCanon");
            if (textura != null)
            {
                textura.filterMode = FilterMode.Point;
                spriteCreado = Sprite.Create(textura, new Rect(0, 0, textura.width, textura.height),
                    Vector2.one * 0.5f, 100f);
                fondo = spriteCreado;
            }
        }
        if (fondo == null || jugador == null)
        {
            Debug.LogError("HUD Canon: asigna Jugador e importa PanelCanon.png en Assets/Resources/HUDCanon.", this);
            enabled = false;
            return;
        }
        canvas = UIBasicaJuego.CrearCanvas("Canvas_Municion", transform, 100);
        imagenPanel = UIBasicaJuego.Imagen("HUD_Canon_Pixelart", canvas.transform,
            new Vector2(384f, 96f), Vector2.zero, ancla, Color.white);
        imagenPanel.sprite = fondo;
        panel = imagenPanel.rectTransform;
        ActualizarPosicion();
        ActualizarContador();
        ActualizarVisibilidad();
    }

    private void LateUpdate()
    {
        if (canvas == null || jugador == null)
            return;
        ActualizarPosicion();
        ActualizarContador();
        ActualizarVisibilidad();
    }

    private void ActualizarPosicion()
    {
        Vector2 punto = new Vector2(Mathf.Clamp01(ancla.x), Mathf.Clamp01(ancla.y));
        panel.anchorMin = panel.anchorMax = panel.pivot = punto;
        panel.anchoredPosition = new Vector2(
            margen.x * (1f - 2f * punto.x), margen.y * (1f - 2f * punto.y));
        panel.localScale = Vector3.one * Mathf.Max(0.25f, escala);
        imagenPanel.sprite = fondo;
    }

    private void ActualizarContador()
    {
        int actual = Mathf.Max(0, jugador.MunicionActualCanon);
        int capacidad = jugador.CapacidadMunicionCanon;
        Color color = actual > 0 ? colorCargado : colorVacio;
        if (actual == ultimaMunicion && capacidad == ultimaCapacidad && color == ultimoColor)
            return;
        ultimaMunicion = actual;
        ultimaCapacidad = capacidad;
        ultimoColor = color;
        string texto = actual.ToString("D2") + "/" + capacidad.ToString("D2");
        int columnas = texto.Length * 4 - 1;
        int celda = Mathf.Clamp(Mathf.FloorToInt(232f / columnas), 1, 6);
        float izquierda = Mathf.Round(230f - columnas * celda * 0.5f);
        float arriba = Mathf.Round(54f - 5f * celda * 0.5f);
        int usados = 0;
        for (int i = 0; i < texto.Length; i++)
        {
            string patron = texto[i] == '/' ? barra : digitos[texto[i] - '0'];
            for (int fila = 0; fila < 5; fila++)
                for (int columna = 0; columna < 3; columna++)
                {
                    if (patron[fila * 3 + columna] != '1')
                        continue;
                    if (usados == pixeles.Count)
                        pixeles.Add(UIBasicaJuego.Imagen("Pixel_" + usados, panel,
                            Vector2.one, Vector2.zero, new Vector2(0f, 1f), color));
                    Image pixel = pixeles[usados++];
                    pixel.gameObject.SetActive(true);
                    pixel.color = color;
                    pixel.rectTransform.sizeDelta = Vector2.one * celda;
                    pixel.rectTransform.anchoredPosition = new Vector2(
                        izquierda + (i * 4 + columna) * celda, -(arriba + fila * celda));
                }
        }
        for (int i = usados; i < pixeles.Count; i++)
            pixeles[i].gameObject.SetActive(false);
    }

    private void ActualizarVisibilidad()
    {
        if (canvas != null)
            canvas.enabled = visible && isActiveAndEnabled &&
                (!ocultarDuranteSecuencias || jugador == null || !jugador.EnSecuencia);
    }

    public void Mostrar(bool valor)
    {
        visible = valor;
        ActualizarVisibilidad();
    }

    private void OnEnable()
    {
        ActualizarVisibilidad();
    }

    private void OnDisable()
    {
        if (canvas != null)
            canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null)
            Destroy(canvas.gameObject);
        if (spriteCreado != null)
            Destroy(spriteCreado);
    }
}
