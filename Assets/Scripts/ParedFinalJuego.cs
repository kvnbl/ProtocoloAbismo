using UnityEngine;

[DisallowMultipleComponent]
public class ParedFinalJuego : MonoBehaviour
{
    public GameObject grupoPared;
    public Collider2D colliderPared;
    public AtaqueRocasGusano referenciaEfectos;
    public bool Rota { get; private set; }
    public Bounds Limites => colliderPared != null ? colliderPared.bounds : new Bounds(transform.position, Vector3.one);
    public GameObject Grupo => grupoPared != null ? grupoPared : gameObject;

    private void Awake()
    {
        if (colliderPared == null)
            colliderPared = Grupo.GetComponentInChildren<Collider2D>();
        if (referenciaEfectos == null)
            referenciaEfectos = FindFirstObjectByType<AtaqueRocasGusano>();
    }

    public void Romper()
    {
        if (Rota)
            return;
        Rota = true;
        Bounds limites = Limites;
        if (referenciaEfectos != null && referenciaEfectos.hojaEfectos != null && referenciaEfectos.prefabRoca != null)
            for (int i = 0; i < 3; i++)
            {
                Vector3 punto = new Vector3(limites.center.x,
                    Mathf.Lerp(limites.min.y, limites.max.y, (i + 0.5f) / 3f), transform.position.z);
                GameObject efecto = EfectoRocaJefe.Crear(referenciaEfectos.hojaEfectos, 2,
                    punto, Mathf.Clamp(limites.size.y * 0.6f, 1.2f, 4f),
                    referenciaEfectos.prefabRoca.Imagen, 0.5f, false);
                if (efecto != null)
                    UtilidadAtaquesJefe.Ordenar(efecto.GetComponent<SpriteRenderer>(), "Efectos", 150);
            }
        AudioJuegoSimple.Instancia.Derrumbe();
        Grupo.SetActive(false);
    }
}
