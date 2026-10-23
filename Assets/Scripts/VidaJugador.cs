using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class VidaJugador : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMaxima = 5;
    public float invulnerabilidad = 0.8f;

    public int VidaActual { get; private set; }
    public bool ProtegidoPorSecuencia { get; set; }

    private float siguienteGolpe;
    private bool muerto;

    private void Awake()
    {
        VidaActual = vidaMaxima;
    }

    public void RecibirDanio(int cantidad)
    {
        if (muerto ||
            ProtegidoPorSecuencia ||
            cantidad <= 0 ||
            Time.time < siguienteGolpe)
        {
            return;
        }

        VidaActual = Mathf.Max(0, VidaActual - cantidad);
        siguienteGolpe = Time.time + invulnerabilidad;

        AudioJuegoSimple.Instancia.Impacto();

        if (VidaActual == 0)
        {
            Morir();
        }
    }

    public void MorirFueraDeCamara()
    {
        ExcavadoraController excavadora =
            GetComponent<ExcavadoraController>();

        if (muerto ||
            ProtegidoPorSecuencia ||
            (excavadora != null && excavadora.EnSecuencia))
        {
            return;
        }

        Morir();
    }

    private void Morir()
    {
        if (muerto)
        {
            return;
        }

        muerto = true;
        VidaActual = 0;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }
}