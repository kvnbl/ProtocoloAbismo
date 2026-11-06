using System.Collections.Generic;
using UnityEngine;

// Se coloca en el objeto que contiene un tramo repetible del fondo.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
[DefaultExecutionOrder(200)]
public class ParallaxInfinito : MonoBehaviour
{
    [SerializeField] private Camera camara;

    [Tooltip("0: fijo en el mundo. 1: se mueve con la cámara. Prueba 0.75 para un fondo lejano.")]
    [SerializeField, Range(0f, 1f)] private float seguimientoHorizontal = 0.75f;

    [Tooltip("0: conserva la altura del mapa. 1: acompaña todo el movimiento vertical de la cámara.")]
    [SerializeField, Range(0f, 1f)] private float seguimientoVertical;

    private readonly List<SpriteRenderer> copias = new List<SpriteRenderer>();
    private SpriteRenderer original;
    private Vector3 posicionInicial;
    private Vector3 posicionInicialCamara;
    private float ancho;
    private float desfaseCentro;
    private bool listo;
    private int paresActivos;

    private void Start()
    {
        original = GetComponent<SpriteRenderer>();
        if (camara == null)
            camara = Camera.main;

        if (camara == null || !camara.orthographic || original.sprite == null)
        {
            Debug.LogWarning("ParallaxInfinito necesita un sprite y una cámara ortográfica.", this);
            enabled = false;
            return;
        }

        Bounds limites = original.bounds;
        ancho = limites.size.x;
        if (ancho <= 0.0001f)
        {
            Debug.LogWarning("El sprite del fondo debe tener un ancho mayor que cero.", this);
            enabled = false;
            return;
        }

        posicionInicial = transform.position;
        posicionInicialCamara = camara.transform.position;
        desfaseCentro = limites.center.x - posicionInicial.x;
        listo = true;
        ActualizarFondo();
    }

    private void LateUpdate()
    {
        if (listo && camara != null)
            ActualizarFondo();
    }

    private void ActualizarFondo()
    {
        Vector3 posicionCamara = camara.transform.position;
        Vector3 desplazamiento = posicionCamara - posicionInicialCamara;
        Vector3 posicion = posicionInicial;
        posicion.x += desplazamiento.x * seguimientoHorizontal;
        posicion.y += desplazamiento.y * seguimientoVertical;

        // Elegimos el tramo cuyo centro queda más cerca de la cámara.
        // Al pasar al tramo vecino cambia la posición del grupo, pero las imágenes coinciden.
        float centro = posicion.x + desfaseCentro;
        float tramos = Mathf.Round((posicionCamara.x - centro) / ancho);
        posicion.x += tramos * ancho;
        transform.position = posicion;

        float medioAnchoCamara = camara.orthographicSize * camara.aspect;
        int paresNecesarios = Mathf.CeilToInt(medioAnchoCamara / ancho);
        AjustarCopias(paresNecesarios);

        for (int i = 0; i < paresActivos * 2; i++)
        {
            int distancia = i / 2 + 1;
            int lado = i % 2 == 0 ? -1 : 1;
            copias[i].transform.position = posicion + Vector3.right * (lado * distancia * ancho);
            copias[i].enabled = original.enabled;
        }
    }

    private void AjustarCopias(int paresNecesarios)
    {
        int cantidad = paresNecesarios * 2;
        while (copias.Count < cantidad)
            copias.Add(CrearCopia());

        for (int i = 0; i < copias.Count; i++)
        {
            bool activa = i < cantidad;
            if (copias[i].gameObject.activeSelf != activa)
                copias[i].gameObject.SetActive(activa);
        }

        paresActivos = paresNecesarios;
    }

    private SpriteRenderer CrearCopia()
    {
        GameObject objeto = new GameObject("Tramo de fondo");
        objeto.layer = gameObject.layer;
        objeto.transform.SetParent(transform, false);

        SpriteRenderer copia = objeto.AddComponent<SpriteRenderer>();
        copia.sprite = original.sprite;
        copia.drawMode = original.drawMode;
        copia.size = original.size;
        copia.tileMode = original.tileMode;
        copia.adaptiveModeThreshold = original.adaptiveModeThreshold;
        copia.color = original.color;
        copia.sharedMaterials = original.sharedMaterials;
        copia.flipX = original.flipX;
        copia.flipY = original.flipY;
        copia.maskInteraction = original.maskInteraction;
        copia.spriteSortPoint = original.spriteSortPoint;
        copia.sortingLayerID = original.sortingLayerID;
        copia.sortingOrder = original.sortingOrder;
        return copia;
    }

    private void OnDisable()
    {
        foreach (SpriteRenderer copia in copias)
        {
            if (copia != null)
                copia.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer copia in copias)
        {
            if (copia != null)
                Destroy(copia.gameObject);
        }
    }
}
