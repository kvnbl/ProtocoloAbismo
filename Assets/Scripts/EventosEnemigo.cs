using UnityEngine;

public class EventosEnemigo : MonoBehaviour
{
    private EnemigoBasico enemigo;

    private void Awake()
    {
        enemigo = GetComponentInParent<EnemigoBasico>();
    }

    public void Saltar()
    {
        if (enemigo != null)
        {
            enemigo.SaltarDesdeAnimacion();
        }
    }
}