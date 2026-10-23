using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ExcavadoraController), typeof(Rigidbody2D))]
public class AudioExcavadora : MonoBehaviour
{
    private ExcavadoraController jugador;
    private Rigidbody2D cuerpo;
    private VidaJugador vida;

    private void Awake()
    {
        jugador = GetComponent<ExcavadoraController>();
        cuerpo = GetComponent<Rigidbody2D>();
        vida = GetComponent<VidaJugador>();
    }

    private void LateUpdate()
    {
        bool activo = jugador.isActiveAndEnabled && cuerpo.simulated &&
            Time.timeScale > 0f && (vida == null || vida.VidaActual > 0);
        float velocidad = Mathf.Abs(cuerpo.linearVelocity.x) /
            Mathf.Max(0.1f, jugador.velocidadMaxima);
        AudioJuegoSimple.Instancia.SostenerMotor(activo, velocidad);
    }

    private void OnDisable()
    {
        if (AudioJuegoSimple.Existente != null)
            AudioJuegoSimple.Existente.SostenerMotor(false, 0f);
    }
}
