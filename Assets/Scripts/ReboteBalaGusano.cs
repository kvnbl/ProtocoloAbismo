using UnityEngine;

public class ReboteBalaGusano : MonoBehaviour
{
    private SpriteRenderer imagen;
    private Vector2 velocidad;
    private Color colorInicial;
    private float edad;
    private const float Duracion = 0.18f;

    public static void Crear(SpriteRenderer original, Vector2 punto,
        Vector2 direccion, Vector2 normal)
    {
        if (original == null || original.sprite == null)
            return;
        Vector2 rebote = Vector2.Reflect(direccion.normalized, normal.normalized);
        if (rebote.sqrMagnitude < 0.001f)
            rebote = -direccion.normalized;
        GameObject objeto = new GameObject("Rebote_Visual_Bala");
        objeto.transform.position = new Vector3(punto.x, punto.y, original.transform.position.z);
        objeto.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(rebote.y, rebote.x) * Mathf.Rad2Deg);
        objeto.transform.localScale = original.transform.lossyScale;
        SpriteRenderer sr = objeto.AddComponent<SpriteRenderer>();
        sr.sprite = original.sprite;
        sr.sharedMaterial = original.sharedMaterial;
        sr.color = original.color;
        sr.flipX = original.flipX;
        sr.flipY = original.flipY;
        sr.sortingLayerID = original.sortingLayerID;
        sr.sortingOrder = original.sortingOrder + 1;
        ReboteBalaGusano efecto = objeto.AddComponent<ReboteBalaGusano>();
        efecto.imagen = sr;
        efecto.colorInicial = sr.color;
        efecto.velocidad = rebote * 8f;
        Destroy(objeto, Duracion);
    }

    private void Update()
    {
        edad += Time.deltaTime;
        transform.position += (Vector3)(velocidad * Time.deltaTime);
        Color color = colorInicial;
        color.a *= Mathf.Clamp01(1f - edad / Duracion);
        imagen.color = color;
    }
}
