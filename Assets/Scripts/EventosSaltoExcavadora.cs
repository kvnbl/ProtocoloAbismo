using UnityEngine;

// Añadir al mismo objeto que tiene el Animator de la excavadora.
[DisallowMultipleComponent]
public class EventosSaltoExcavadora : MonoBehaviour
{
    [SerializeField] private ExcavadoraController controlador;

    private void Awake()
    {
        if (controlador == null)
        {
            controlador =
                GetComponentInParent<ExcavadoraController>();
        }

        if (controlador == null)
        {
            Debug.LogError(
                "Asigna el ExcavadoraController a EventosSaltoExcavadora.",
                this
            );
        }
    }

    // Selecciona esta función en el evento de la animación.
    public void EventoDespegue()
    {
        if (controlador != null)
        {
            controlador.SolicitarDespegueDesdeAnimacion();
        }
    }
}