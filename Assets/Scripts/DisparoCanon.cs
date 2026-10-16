using UnityEngine;

public class DisparoCanon : MonoBehaviour
{
    [Header("Proyectil")]
    public ProyectilCanon proyectilPrefab;
    public Transform puntoDisparoCanon;
    public float velocidadSalida = 12f;

    [Header("Vehículo que dispara")]
    public Transform propietario;

    [Header("Sonido opcional")]
    public AudioSource fuenteAudio;
    public AudioClip sonidoDisparo;

    private void Awake()
    {
        if (propietario == null)
        {
            ExcavadoraController controlador =
                GetComponentInParent<ExcavadoraController>();

            if (controlador != null)
            {
                propietario = controlador.transform;
            }
        }
    }

    // Esta función se selecciona en el Animation Event.
    public void EventoDisparoCanon()
    {
        if (proyectilPrefab == null ||
            puntoDisparoCanon == null)
        {
            Debug.LogWarning(
                "Asigna el prefab y el punto de disparo del cañón.",
                this
            );

            return;
        }

        // TransformVector también contempla un vehículo reflejado.
        Vector2 direccion = puntoDisparoCanon
            .TransformVector(Vector3.right);

        direccion.Normalize();

        ProyectilCanon nuevo = Instantiate(
            proyectilPrefab,
            puntoDisparoCanon.position,
            Quaternion.identity
        );

        nuevo.Lanzar(
            direccion * velocidadSalida,
            propietario
        );

        if (fuenteAudio != null && sonidoDisparo != null)
        {
            fuenteAudio.PlayOneShot(sonidoDisparo);
        }
    }
}