using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(BoxCollider2D))]
public class TaladroDestructivo : MonoBehaviour
{
    [Header("Rocas que puede romper")]
    public Tilemap rocasDestructibles;

    [Header("Tiempo entre perforaciones")]
    public float intervalo = 0.12f;

    private BoxCollider2D zona;
    private bool activo;
    private float proximaPerforacion;

    private void Awake()
    {
        zona = GetComponent<BoxCollider2D>();
        zona.isTrigger = true;
    }

    public void EstablecerActivo(bool nuevoEstado)
    {
        if (nuevoEstado && !activo)
        {
            proximaPerforacion = 0f;
        }

        activo = nuevoEstado;
    }

    private void OnDisable()
    {
        activo = false;
    }

    private void FixedUpdate()
    {
        if (!activo ||
            rocasDestructibles == null ||
            !zona.enabled)
        {
            return;
        }

        if (Time.time < proximaPerforacion)
            return;

        proximaPerforacion = Time.time + intervalo;

        Bounds limites = zona.bounds;

        // Evita incluir una celda vecina por tocar solo su límite.
        Vector3 margen = new Vector3(0.001f, 0.001f, 0f);

        Vector3Int minimo = rocasDestructibles.WorldToCell(
            limites.min + margen
        );

        Vector3Int maximo = rocasDestructibles.WorldToCell(
            limites.max - margen
        );

        for (int x = minimo.x; x <= maximo.x; x++)
        {
            for (int y = minimo.y; y <= maximo.y; y++)
            {
                Vector3Int celda = new Vector3Int(
                    x,
                    y,
                    minimo.z
                );

                if (rocasDestructibles.HasTile(celda))
                {
                    rocasDestructibles.SetTile(celda, null);
                }
            }
        }
    }
}