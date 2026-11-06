using UnityEngine;

// Responsabilidad: Actualiza el sonido del motor segun el estado y la velocidad del jugador.
[DisallowMultipleComponent]
[RequireComponent(typeof(ExcavadoraController), typeof(Rigidbody2D))]
public class AudioExcavadora : MonoBehaviour
{

    #region Datos, configuracion y estado

    private ExcavadoraController jugador;

    private Rigidbody2D cuerpo;

    private VidaJugador vida;

    #endregion

    #region Inicializacion y activacion

    private void Awake()
    {
        jugador = GetComponent<ExcavadoraController>();
        cuerpo = GetComponent<Rigidbody2D>();
        vida = GetComponent<VidaJugador>();
    }

    #endregion

    #region Actualizacion durante el juego

    private void LateUpdate()
    {
        bool activo = jugador.isActiveAndEnabled && cuerpo.simulated &&
            Time.timeScale > 0f && (vida == null || vida.VidaActual > 0);
        float velocidad = Mathf.Abs(cuerpo.linearVelocity.x) /
            Mathf.Max(0.1f, jugador.velocidadMaxima);
        AudioJuegoSimple.Instancia.SostenerMotor(activo, velocidad);
    }

    #endregion

    #region Desactivacion y limpieza

    private void OnDisable()
    {
        if (AudioJuegoSimple.Existente != null)
            AudioJuegoSimple.Existente.SostenerMotor(false, 0f);
    }

    #endregion

}
