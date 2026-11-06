using UnityEngine;
using UnityEngine.SceneManagement;

// Responsabilidad: Administra la vida, la invulnerabilidad y la derrota del jugador.
[DisallowMultipleComponent]
public class VidaJugador : MonoBehaviour
{

    #region Datos, configuracion y estado

    public int vidaMaxima = 5;

    public float invulnerabilidad = 0.8f;

    public int VidaActual { get; private set; }

    private float siguienteGolpe;

    private bool muerto;

    public bool ProtegidoPorSecuencia { get; set; }

    #endregion

    #region Inicializacion y activacion

    private void Awake()
    {
        VidaActual = vidaMaxima;
    }

    #endregion

    #region Acciones disponibles para otros componentes

    public void MorirFueraDeCamara()
    {
        ExcavadoraController jugador = GetComponent<ExcavadoraController>();
        if (muerto || ProtegidoPorSecuencia || (jugador != null && jugador.EnSecuencia))
            return;
        muerto = true;
        VidaActual = 0;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void RecibirDanio(int cantidad)
    {
        if (ProtegidoPorSecuencia || muerto || cantidad <= 0 || Time.time < siguienteGolpe)
            return;

        VidaActual = Mathf.Max(0, VidaActual - cantidad);
        siguienteGolpe = Time.time + invulnerabilidad;
        AudioJuegoSimple.Instancia.Impacto();

        Debug.Log("Jugador: quedan " + VidaActual + " vidas.", this);

        if (VidaActual == 0)
        {
            muerto = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    #endregion

}
