using UnityEngine;
using UnityEngine.UI;

public static class UIBasicaJuego
{
    public static Canvas CrearCanvas(string nombre, Transform padre, int orden)
    {
        GameObject objeto = new GameObject(nombre, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        objeto.transform.SetParent(padre, false);
        Canvas canvas = objeto.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = orden;
        CanvasScaler escala = objeto.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static RectTransform Rectangulo(string nombre, Transform padre, Vector2 tamano,
        Vector2 posicion, Vector2 ancla)
    {
        RectTransform rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        rect.anchorMin = rect.anchorMax = ancla;
        rect.pivot = ancla;
        rect.sizeDelta = tamano;
        rect.anchoredPosition = posicion;
        return rect;
    }

    public static Image Imagen(string nombre, Transform padre, Vector2 tamano,
        Vector2 posicion, Vector2 ancla, Color color)
    {
        RectTransform rect = Rectangulo(nombre, padre, tamano, posicion, ancla);
        Image imagen = rect.gameObject.AddComponent<Image>();
        imagen.color = color;
        imagen.raycastTarget = false;
        return imagen;
    }

    public static Text Texto(string nombre, Transform padre, Vector2 tamano, Vector2 posicion,
        Vector2 ancla, string contenido, int fuente, TextAnchor alineacion)
    {
        RectTransform rect = Rectangulo(nombre, padre, tamano, posicion, ancla);
        Text texto = rect.gameObject.AddComponent<Text>();
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = fuente;
        texto.alignment = alineacion;
        texto.color = Color.white;
        texto.text = contenido;
        texto.raycastTarget = false;
        return texto;
    }
}
