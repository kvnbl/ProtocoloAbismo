using System.Collections.Generic;
using UnityEngine;

public static class UtilidadAtaquesJefe
{
    public static void Ordenar(SpriteRenderer imagen, string preferida, int orden)
    {
        if (imagen == null)
            return;
        SortingLayer[] capas = SortingLayer.layers;
        int id = capas.Length > 0 ? capas[capas.Length - 1].id : 0;
        foreach (SortingLayer capa in capas)
            if (capa.name == preferida)
            {
                id = capa.id;
                break;
            }
        imagen.sortingLayerID = id;
        imagen.sortingOrder = orden;
    }

    public static bool Libre(Vector2 punto, float radio, LayerMask terreno)
    {
        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(terreno);
        filtro.useTriggers = false;
        List<Collider2D> resultados = new List<Collider2D>(8);
        return Physics2D.OverlapCircle(punto, radio, filtro, resultados) == 0;
    }

    // Busca junto a la salida y hacia dentro; no hace aparecer peligros al otro lado de un muro.
    public static bool BuscarSalida(Vector2 deseado, Vector2 haciaSala, float radio,
        LayerMask terreno, Camera camara, out Vector2 encontrado)
    {
        encontrado = deseado;
        float paso = Mathf.Max(0.12f, radio * 0.65f);
        for (int avance = 0; avance <= 8; avance++)
            for (int altura = 0; altura <= 3; altura++)
            {
                Vector2 punto = deseado + haciaSala.normalized * paso * avance + Vector2.up * paso * altura;
                if (camara != null && camara.orthographic)
                {
                    Vector2 centro = camara.transform.position;
                    float h = camara.orthographicSize;
                    float w = h * camara.aspect;
                    if (punto.x - radio < centro.x - w + 0.15f ||
                        punto.x + radio > centro.x + w - 0.15f ||
                        punto.y - radio < centro.y - h + 0.15f ||
                        punto.y + radio > centro.y + h - 0.15f)
                        continue;
                }
                if (Libre(punto, radio + 0.04f, terreno))
                {
                    encontrado = punto;
                    return true;
                }
            }
        return false;
    }
}
