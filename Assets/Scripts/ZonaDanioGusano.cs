using UnityEngine;

public enum TipoAtaqueGusano { Bala, Taladro, Canon }
public enum ParteGusano { Caparazon, Vientre }

[DisallowMultipleComponent]
public class ZonaDanioGusano : MonoBehaviour
{
    [SerializeField] private ParteGusano parte;
    private Collider2D zona;
    public VidaGusano Vida { get; private set; }
    public ParteGusano Parte => parte;
    public bool EstaActiva => Vida != null && Vida.Disponible &&
        zona != null && zona.enabled && gameObject.activeInHierarchy;

    private void Awake()
    {
        zona = GetComponent<Collider2D>();
        Vida = GetComponentInParent<VidaGusano>();
    }

    public void Configurar(VidaGusano vida, ParteGusano tipo)
    {
        Vida = vida;
        parte = tipo;
        zona = GetComponent<Collider2D>();
    }

    public float CalcularDanio(int cantidad, TipoAtaqueGusano ataque)
    {
        return EstaActiva ? Vida.CalcularDanio(cantidad, ataque, parte) : 0f;
    }

    public void RecibirDanio(int cantidad, TipoAtaqueGusano ataque)
    {
        if (EstaActiva)
            Vida.RecibirDanio(CalcularDanio(cantidad, ataque));
    }

    public void DibujarGizmo()
    {
        PolygonCollider2D poligono = GetComponent<PolygonCollider2D>();
        if (poligono == null || !poligono.enabled)
            return;
        Gizmos.color = parte == ParteGusano.Caparazon ? Color.yellow : Color.green;
        Vector2[] puntos = poligono.points;
        for (int i = 0; i < puntos.Length; i++)
            Gizmos.DrawLine(transform.TransformPoint(puntos[i]),
                transform.TransformPoint(puntos[(i + 1) % puntos.Length]));
    }
}
