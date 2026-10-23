using UnityEngine;

[DisallowMultipleComponent]
public class VisualAcidoGusano : MonoBehaviour
{
    private readonly Sprite[] cuadros = new Sprite[4];
    private SpriteRenderer imagen;
    private float tiempo;
    private float duracion;
    private bool bucle;

    public void Configurar(Texture2D hoja, int fila, float segundos, bool repetir,
        string capaDibujo, int orden)
    {
        imagen = GetComponent<SpriteRenderer>();
        if (imagen == null)
            imagen = gameObject.AddComponent<SpriteRenderer>();
        duracion = Mathf.Max(0.05f, segundos);
        bucle = repetir;
        if (hoja == null || hoja.width != 512 || hoja.height != 256)
        {
            enabled = false;
            return;
        }
        for (int i = 0; i < 4; i++)
            cuadros[i] = Sprite.Create(hoja, new Rect(i * 128, 256 - (fila + 1) * 128, 128, 128),
                Vector2.one * 0.5f, 128f, 0, SpriteMeshType.FullRect);
        imagen.sprite = cuadros[0];
        UtilidadAtaquesJefe.Ordenar(imagen, capaDibujo, orden);
    }

    private void Update()
    {
        tiempo += Time.deltaTime;
        if (!bucle && tiempo >= duracion)
        {
            Destroy(gameObject);
            return;
        }
        int indice = bucle ? Mathf.FloorToInt(tiempo * 12f) % 4
            : Mathf.Min(3, Mathf.FloorToInt(tiempo / duracion * 4f));
        if (imagen != null && cuadros[indice] != null)
            imagen.sprite = cuadros[indice];
    }

    private void OnDestroy()
    {
        foreach (Sprite cuadro in cuadros)
            if (cuadro != null)
                Destroy(cuadro);
    }

    public static GameObject CrearEfecto(Texture2D hoja, int fila, Vector3 punto,
        float ancho, float segundos, bool bucle, string capaDibujo, int orden)
    {
        GameObject objeto = new GameObject(fila == 0 ? "Carga_Acido" : "Salpicadura_Acido");
        objeto.transform.position = punto;
        objeto.transform.localScale = Vector3.one * ancho;
        objeto.AddComponent<VisualAcidoGusano>().Configurar(hoja, fila, segundos, bucle, capaDibujo, orden);
        return objeto;
    }
}
