using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PantallaTransicionJuego : MonoBehaviour
{
    private static PantallaTransicionJuego instancia;
    public static PantallaTransicionJuego Instancia
    {
        get
        {
            if (instancia == null)
                instancia = FindFirstObjectByType<PantallaTransicionJuego>();
            if (instancia == null)
                instancia = new GameObject("Transicion_Pantalla").AddComponent<PantallaTransicionJuego>();
            return instancia;
        }
    }
    private Image negro;
    private Text titulo;
    private Text subtitulo;

    private void Awake()
    {
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        instancia = this;
        Canvas canvas = UIBasicaJuego.CrearCanvas("Canvas_Transicion", transform, 1000);
        negro = UIBasicaJuego.Imagen("Negro", canvas.transform, Vector2.zero,
            Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0));
        RectTransform rect = negro.rectTransform;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        titulo = UIBasicaJuego.Texto("Fin", canvas.transform, new Vector2(1600, 140),
            new Vector2(0, 30), Vector2.one * 0.5f, "", 64, TextAnchor.MiddleCenter);
        subtitulo = UIBasicaJuego.Texto("Subtitulo", canvas.transform, new Vector2(1200, 80),
            new Vector2(0, -80), Vector2.one * 0.5f, "", 28, TextAnchor.MiddleCenter);
    }

    public IEnumerator Fundir(float destino, float segundos)
    {
        float origen = negro.color.a;
        float tiempo = 0f;
        while (tiempo < segundos)
        {
            tiempo += Time.unscaledDeltaTime;
            FijarNegro(Mathf.Lerp(origen, destino, tiempo / Mathf.Max(0.01f, segundos)));
            yield return null;
        }
        FijarNegro(destino);
    }

    public void FijarNegro(float alfa)
    {
        if (negro != null)
            negro.color = new Color(0, 0, 0, Mathf.Clamp01(alfa));
    }
    public void MostrarFinal(string texto, string detalle)
    {
        FijarNegro(1f);
        titulo.text = texto;
        subtitulo.text = detalle;
    }
    public void OcultarFinal()
    {
        titulo.text = "";
        subtitulo.text = "";
        FijarNegro(0f);
    }
    private void OnDestroy()
    {
        if (instancia == this)
            instancia = null;
    }
}
