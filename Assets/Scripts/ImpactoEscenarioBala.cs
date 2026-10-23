using UnityEngine;

[DisallowMultipleComponent]
public class ImpactoEscenarioBala : MonoBehaviour
{
    private SpriteRenderer imagen;
    private Sprite[] cuadros;
    private float fps;
    private float tiempo;

    public static void Crear(
        Texture2D hoja, Vector2 punto, Vector2 normal,
        float tamano, float cuadrosPorSegundo,
        int capaOrden, int orden, int capaObjeto, float profundidad,
        Material material = null)
    {
        if (hoja == null) return;
        if (hoja.width != 512 || hoja.height != 256)
        {
            Debug.LogWarning(
                "ImpactoEscenarioBala: la hoja debe medir512x256.");
            return;
        }

        if (normal.sqrMagnitude < 0.000001f)
            normal = Vector2.right;
        normal.Normalize();

        GameObject objeto = new GameObject("ImpactoEscenarioBala");
        objeto.layer = capaObjeto;
        Vector2 posicion = punto + normal * 0.01f;
        objeto.transform.position = new Vector3(posicion.x, posicion.y, profundidad);
        objeto.transform.rotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg);
        objeto.transform.localScale = Vector3.one * Mathf.Max(0.05f, tamano);

        SpriteRenderer renderer = objeto.AddComponent<SpriteRenderer>();
        if (material != null) renderer.sharedMaterial = material;
        renderer.sortingLayerID = capaOrden;
        renderer.sortingOrder = orden;

        ImpactoEscenarioBala efecto = objeto.AddComponent<ImpactoEscenarioBala>();
        efecto.imagen = renderer;
        efecto.fps = Mathf.Max(1f, cuadrosPorSegundo);
        efecto.cuadros = new Sprite[8];
        for (int i = 0; i < efecto.cuadros.Length; i++)
        {
            efecto.cuadros[i] = Sprite.Create(
                hoja, new Rect(i % 4 * 128, (1 - i / 4) * 128, 128, 128),
                new Vector2(0.125f, 0.5f), 128f, 0, SpriteMeshType.FullRect);
        }
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
