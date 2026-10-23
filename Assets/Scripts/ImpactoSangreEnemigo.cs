using UnityEngine;

public class ImpactoSangreEnemigo : MonoBehaviour
{
    private Sprite[] cuadros;
    private SpriteRenderer imagen;
    private float fps;
    private float tiempo;

    public static void Crear(Texture2D hoja, Vector2 punto, Vector2 salida,
        SpriteRenderer referencia, float cuadrosPorSegundo)
    {
        if (hoja == null || referencia == null || referencia.sprite == null) return;
        if (hoja.width != 256 || hoja.height != 128)
        {
            Debug.LogWarning("La hoja de sangre debe conservar 256x128.");
            return;
        }
        GameObject objeto = new GameObject("ImpactoSangre");
        objeto.layer = referencia.gameObject.layer;
        objeto.transform.position = new Vector3(punto.x, punto.y, referencia.transform.position.z);
        if (salida.sqrMagnitude < 0.001f) salida = Vector2.right;
        objeto.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(salida.y, salida.x) * Mathf.Rad2Deg);
        Vector3 escala = referencia.transform.lossyScale;

        // 1 = tamaño original, 2 = doble, 3 = triple.
        float tamanoEfecto = 3f;

        objeto.transform.localScale = new Vector3(
            Mathf.Abs(escala.x) * tamanoEfecto,
            Mathf.Abs(escala.y) * tamanoEfecto,
            1f
        );
        SpriteRenderer renderer = objeto.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = referencia.sharedMaterial;
        renderer.sortingLayerID = referencia.sortingLayerID;
        renderer.sortingOrder = referencia.sortingOrder + 2;
        ImpactoSangreEnemigo efecto = objeto.AddComponent<ImpactoSangreEnemigo>();
        efecto.imagen = renderer;
        efecto.fps = Mathf.Max(1f, cuadrosPorSegundo);
        efecto.cuadros = new Sprite[8];
        for (int i = 0; i < 8; i++)
            efecto.cuadros[i] = Sprite.Create(hoja,
                new Rect(i % 4 * 64, (1 - i / 4) * 64, 64, 64),
                new Vector2(0.125f, 0.5f), referencia.sprite.pixelsPerUnit,
                0, SpriteMeshType.FullRect);
        renderer.sprite = efecto.cuadros[0];
    }

    private void Update()
    {
        if (cuadros == null) return;
        tiempo += Time.deltaTime;
        int cuadro = Mathf.FloorToInt(tiempo * fps);
        if (cuadro >= cuadros.Length)
        {
            imagen.enabled = false;
            Destroy(gameObject);
        }
        else imagen.sprite = cuadros[cuadro];
    }

    private void OnDestroy()
    {
        if (cuadros == null) return;
        foreach (Sprite cuadro in cuadros)
            if (cuadro != null) Destroy(cuadro);
    }
}
