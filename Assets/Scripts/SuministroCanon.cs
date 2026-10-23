using UnityEngine;

[DisallowMultipleComponent]
public class SuministroCanon : MonoBehaviour
{
    [Min(1)] public int cantidad = 3;

    private bool recogido;

    private void OnTriggerEnter2D(Collider2D otro)
    {
        IntentarRecoger(otro);
    }

    private void OnTriggerStay2D(Collider2D otro)
    {
        IntentarRecoger(otro);
    }

    private void IntentarRecoger(Collider2D otro)
    {
        if (recogido || otro.isTrigger)
            return;

        ExcavadoraController jugador =
            otro.GetComponentInParent<ExcavadoraController>();

        if (jugador == null || !jugador.isActiveAndEnabled)
            return;

        int recibido = jugador.RecargarCanon(cantidad);

        // Si está lleno, conserva el suministro.
        if (recibido <= 0)
            return;

        // Evita recogerlo varias veces por los colliders del vehículo.
        recogido = true;
        Destroy(gameObject);
    }
}