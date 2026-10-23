using UnityEngine;
using UnityEngine.SceneManagement;

public class ZonaMuerte : MonoBehaviour
{
    public Camera camaraJuego;
    public Collider2D colliderCuerpo;

    private readonly Plane[] planos = new Plane[6];

    private bool estuvoEnPantalla;
    private bool reiniciando;

    private void Awake()
    {
        if (camaraJuego == null)
            camaraJuego = Camera.main;

        if (colliderCuerpo == null)
            colliderCuerpo = GetComponent<Collider2D>();
    }

    private void LateUpdate()
    {
        if (reiniciando ||
            camaraJuego == null ||
            colliderCuerpo == null ||
            !colliderCuerpo.enabled)
        {
            return;
        }

        GeometryUtility.CalculateFrustumPlanes(
            camaraJuego,
            planos
        );

        bool dentroDePantalla =
            GeometryUtility.TestPlanesAABB(
                planos,
                colliderCuerpo.bounds
            );

        if (dentroDePantalla)
        {
            estuvoEnPantalla = true;
            return;
        }

        // Espera a que el jugador haya aparecido al menos una vez.
        if (!estuvoEnPantalla)
            return;

        reiniciando = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }
}