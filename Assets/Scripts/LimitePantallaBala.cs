using UnityEngine;

public class LimitePantallaBala : MonoBehaviour
{
    private Camera camaraJuego;

    private void Awake()
    {
        camaraJuego = Camera.main;
    }

    public bool EstaDentroDePantalla()
    {
        if (camaraJuego == null)
            return false;

        Vector3 posicion = camaraJuego.WorldToViewportPoint(
            transform.position
        );

        return posicion.z > 0f &&
               posicion.x >= 0f &&
               posicion.x <= 1f &&
               posicion.y >= 0f &&
               posicion.y <= 1f;
    }

    private void LateUpdate()
    {
        if (!EstaDentroDePantalla())
        {
            Destroy(gameObject);
        }
    }
}