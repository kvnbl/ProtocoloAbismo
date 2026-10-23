using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class VidaGusano : MonoBehaviour
{
    [Header("Vida compartida por todo el cuerpo")]
    [Min(1f)] public float vidaMaxima = 60f;
    [SerializeField] private float vidaActual;
    public bool mostrarDanioEnConsole = true;
    public bool mostrarZonas = true;

    [Header("Balas: multiplicadores")]
    [Min(0f)] public float balaVientre = 1f;

    [Header("Taladro: multiplicadores")]
    [Min(0f)] public float taladroCaparazon = 0.5f;
    [Min(0f)] public float taladroVientre = 2f;

    [Header("Canon: multiplicadores")]
    [Min(0f)] public float canonCaparazon = 0.5f;
    [Min(0f)] public float canonVientre = 1.5f;

    private readonly List<ZonaDanioGusano> zonas = new List<ZonaDanioGusano>();
    private bool expuesto;
    private DestelloDanioGusano destello;
    public float VidaActual => vidaActual;
    public bool EstaVivo => vidaActual > 0f;
    public bool Disponible => EstaVivo && expuesto && isActiveAndEnabled;
    public event System.Action Murio;

    private void Awake()
    {
        destello = GetComponent<DestelloDanioGusano>();
        if (destello == null)
            destello = gameObject.AddComponent<DestelloDanioGusano>();
        vidaActual = Mathf.Max(1f, vidaMaxima);
        Rigidbody2D cuerpo = GetComponent<Rigidbody2D>();
        cuerpo.bodyType = RigidbodyType2D.Kinematic;
        cuerpo.gravityScale = 0f;
    }

    public float CalcularDanio(int cantidad, TipoAtaqueGusano ataque, ParteGusano parte)
    {
        bool armadura = parte == ParteGusano.Caparazon;
        float multiplicador;
        switch (ataque)
        {
            case TipoAtaqueGusano.Bala:
                multiplicador = armadura ? 0f : balaVientre;
                break;
            case TipoAtaqueGusano.Taladro:
                multiplicador = armadura ? taladroCaparazon : taladroVientre;
                break;
            default:
                multiplicador = armadura ? canonCaparazon : canonVientre;
                break;
        }
        return Mathf.Max(0, cantidad) * Mathf.Max(0f, multiplicador);
    }

    public void RecibirDanio(float cantidad)
    {
        if (!Disponible || cantidad <= 0f)
            return;
        vidaActual = Mathf.Max(0f, vidaActual - cantidad);
        AudioJuegoSimple.Instancia.GusanoDanio();
        // RecibirDanio descarta el cero de las balas contra el caparazon.
        if (destello != null)
            destello.Mostrar();
        if (mostrarDanioEnConsole)
            Debug.Log("Gusano: " + vidaActual.ToString("0.##") +
                " / " + vidaMaxima.ToString("0.##") + " de vida.", this);
        if (!EstaVivo)
        {
            EstablecerExpuesto(false);
            Murio?.Invoke();
        }
    }

    public void EstablecerExpuesto(bool valor)
    {
        expuesto = valor && EstaVivo;
        foreach (ZonaDanioGusano zona in zonas)
            if (zona != null)
                zona.GetComponent<Collider2D>().enabled = expuesto;
    }

    // Coordenadas del arte: celdas de 512, origen arriba a la izquierda.
    public void CrearZonasPieza(Transform pieza, int indice, float ppu)
    {
        if (indice == 0)
        {
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(118,190),new Vector2(280,96),new Vector2(382,137),
                new Vector2(405,252),new Vector2(370,268),new Vector2(300,238),
                new Vector2(258,220),new Vector2(225,265),new Vector2(186,331),
                new Vector2(153,303),new Vector2(127,257) });
            Crear(pieza, ParteGusano.Vientre, ppu, new Vector2[] {
                new Vector2(228,252),new Vector2(300,241),new Vector2(370,267),
                new Vector2(399,288),new Vector2(360,324),new Vector2(300,334),
                new Vector2(241,321),new Vector2(207,294) });
        }
        else if (indice == 1)
        {
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(120,150),new Vector2(286,96),new Vector2(382,136),
                new Vector2(402,238),new Vector2(368,264),new Vector2(298,238),
                new Vector2(252,212),new Vector2(154,207) });
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(103,183),new Vector2(162,156),new Vector2(230,177),
                new Vector2(213,207),new Vector2(141,244),new Vector2(113,271) });
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(118,280),new Vector2(137,328),new Vector2(182,365),
                new Vector2(248,310),new Vector2(269,266),new Vector2(248,252) });
            Crear(pieza, ParteGusano.Vientre, ppu, new Vector2[] {
                new Vector2(151,246),new Vector2(218,218),new Vector2(295,242),
                new Vector2(370,263),new Vector2(392,290),new Vector2(354,331),
                new Vector2(280,348),new Vector2(239,310),new Vector2(166,290) });
        }
        else if (indice == 9)
        {
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(128,173),new Vector2(198,137),new Vector2(275,177),
                new Vector2(336,210),new Vector2(427,218),new Vector2(387,257),
                new Vector2(318,293),new Vector2(260,319),new Vector2(184,307),new Vector2(128,278) });
            Crear(pieza, ParteGusano.Vientre, ppu, new Vector2[] {
                new Vector2(128,278),new Vector2(184,307),new Vector2(260,319),
                new Vector2(318,293),new Vector2(350,284),new Vector2(310,327),
                new Vector2(234,354),new Vector2(160,353),new Vector2(128,329) });
        }
        else if (indice == 5)
        {
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(196,178),new Vector2(237,145),new Vector2(292,162),
                new Vector2(316,188),new Vector2(316,273),new Vector2(284,294),
                new Vector2(255,274),new Vector2(222,258),new Vector2(196,248) });
            Crear(pieza, ParteGusano.Vientre, ppu, new Vector2[] {
                new Vector2(196,248),new Vector2(222,258),new Vector2(255,274),
                new Vector2(284,294),new Vector2(316,273),new Vector2(316,335),
                new Vector2(288,354),new Vector2(231,360),new Vector2(196,335) });
        }
        else
        {
            Crear(pieza, ParteGusano.Caparazon, ppu, new Vector2[] {
                new Vector2(196,157),new Vector2(225,137),new Vector2(258,151),
                new Vector2(279,166),new Vector2(300,151),new Vector2(316,176),
                new Vector2(316,310),new Vector2(296,290),new Vector2(280,272),
                new Vector2(268,299),new Vector2(260,325),new Vector2(250,310),
                new Vector2(231,282),new Vector2(211,274),new Vector2(196,261) });
            Crear(pieza, ParteGusano.Vientre, ppu, new Vector2[] {
                new Vector2(196,261),new Vector2(211,274),new Vector2(231,282),
                new Vector2(250,310),new Vector2(260,325),new Vector2(268,299),
                new Vector2(280,272),new Vector2(296,290),new Vector2(316,310),
                new Vector2(316,336),new Vector2(292,351),new Vector2(267,360),
                new Vector2(230,359),new Vector2(207,344),new Vector2(196,328) });
        }
    }

    private void Crear(Transform pieza, ParteGusano parte, float ppu, Vector2[] puntos)
    {
        GameObject objeto = new GameObject("Zona_" + parte);
        objeto.layer = gameObject.layer;
        objeto.transform.SetParent(pieza, false);
        for (int i = 0; i < puntos.Length; i++)
            puntos[i] = new Vector2((puntos[i].x - 256f) / ppu, (256f - puntos[i].y) / ppu);
        PolygonCollider2D collider = objeto.AddComponent<PolygonCollider2D>();
        collider.isTrigger = true;
        collider.SetPath(0, puntos);
        collider.enabled = expuesto && EstaVivo;
        ZonaDanioGusano zona = objeto.AddComponent<ZonaDanioGusano>();
        zona.Configurar(this, parte);
        zonas.Add(zona);
    }

    public void ActualizarZonasCabeza(Transform cabeza, int indice, float ppu)
    {
        foreach (ZonaDanioGusano zona in cabeza.GetComponentsInChildren<ZonaDanioGusano>(true))
        {
            zona.GetComponent<Collider2D>().enabled = false;
            zonas.Remove(zona);
            Destroy(zona.gameObject);
        }
        CrearZonasPieza(cabeza, indice, ppu);
    }

    private void OnDrawGizmosSelected()
    {
        if (mostrarZonas)
            foreach (ZonaDanioGusano zona in zonas)
                if (zona != null)
                    zona.DibujarGizmo();
    }
}
