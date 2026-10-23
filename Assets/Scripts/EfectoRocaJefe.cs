using UnityEngine;

// Los efectos se recortan por celdas: no necesitan Animator ni prefab propio.
public class EfectoRocaJefe : MonoBehaviour
{
    private readonly Sprite[] cuadros = new Sprite[4];
    private SpriteRenderer imagen;
    private float duracion;
    private float transcurrido;
    private bool repetir;

    public static GameObject Crear(Texture2D hoja, int fila, Vector3 punto,
        float anchoMundo, SpriteRenderer referencia, float segundos, bool bucle)
    {
        if (hoja == null || hoja.width != 512 || hoja.height != 384 ||
            fila < 1 || fila > 2)
            return null;

        GameObject objeto = new GameObject(fila == 1 ? "Aviso_Roca" : "Rotura_Roca");
        objeto.transform.position = punto;
        objeto.transform.localScale = Vector3.one * Mathf.Max(0.01f, anchoMundo);
        EfectoRocaJefe efecto = objeto.AddComponent<EfectoRocaJefe>();
        efecto.imagen = objeto.AddComponent<SpriteRenderer>();
        if (referencia != null)
        {
            efecto.imagen.sharedMaterial = referencia.sharedMaterial;
            efecto.imagen.sortingLayerID = referencia.sortingLayerID;
            efecto.imagen.sortingOrder = referencia.sortingOrder + 3;
        }
        efecto.duracion = Mathf.Max(0.05f, segundos);
        efecto.repetir = bucle;
        Vector2 pivote = fila == 1 ? new Vector2(0.5f, 0.75f) : Vector2.one * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            efecto.cuadros[i] = Sprite.Create(hoja,
                new Rect(i * 128, 384 - (fila + 1) * 128, 128, 128),
                pivote, 128f, 0, SpriteMeshType.FullRect);
        }
        efecto.imagen.sprite = efecto.cuadros[0];
        return objeto;
    }

    private void Update()
    {
        transcurrido += Time.deltaTime;
        if (transcurrido >= duracion)
        {
            Destroy(gameObject);
            return;
        }
        int indice = repetir
            ? Mathf.FloorToInt(transcurrido * 12f) % 4
            : Mathf.Min(3, Mathf.FloorToInt(transcurrido / duracion * 4f));
        imagen.sprite = cuadros[indice];
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in cuadros)
            if (sprite != null)
                Destroy(sprite);
    }
}
