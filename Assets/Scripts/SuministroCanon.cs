using UnityEngine;

// Responsabilidad: Detecta al jugador, solicita la recarga y consume el suministro si entrega municion.
[DisallowMultipleComponent]
public class SuministroCanon : MonoBehaviour
{

    #region Datos, configuracion y estado

    [Min(1)] public int cantidad = 3;

    private bool recogido;

    #endregion

    #region Deteccion de contactos

    private void OnTriggerEnter2D(Collider2D otro)
    {
        IntentarRecoger(otro);
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        // Si estaba lleno, permite recogerlo al gastar munición sin salir de la caja.
        IntentarRecoger(otro);
    }

    #endregion

    #region Logica interna

    private void IntentarRecoger(Collider2D otro)
    {
        if (recogido || otro.isTrigger)
            return;

        ExcavadoraController jugador =
            otro.GetComponentInParent<ExcavadoraController>();

        if (jugador == null || !jugador.isActiveAndEnabled)
            return;

        int recibido = jugador.RecargarCanon(cantidad);

        // No consume la caja si el cañón ya está lleno.
        if (recibido <= 0)
            return;

        recogido = true;
        Destroy(gameObject);
    }

    #endregion

}
