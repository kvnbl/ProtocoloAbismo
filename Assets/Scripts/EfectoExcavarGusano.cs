using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GusanoModularPrueba))]
public class EfectoExcavarGusano : MonoBehaviour
{
    public string sortingLayerPolvo = "Efectos";
    public int ordenPolvo = 140;
    [Range(0.5f, 2f)] public float tamanoRelativo = 1.2f;
    private GusanoModularPrueba gusano;
    private AtaqueRocasGusano rocas;
    private readonly List<GameObject> efectos = new List<GameObject>();

    private void OnEnable()
    {
        gusano = GetComponent<GusanoModularPrueba>();
        rocas = GetComponent<AtaqueRocasGusano>();
        gusano.AlEnterrarse += PuffFinal;
        gusano.PruebaDetenida += Limpiar;
    }
    private void OnDisable()
    {
        if (gusano != null)
        {
            gusano.AlEnterrarse -= PuffFinal;
            gusano.PruebaDetenida -= Limpiar;
        }
        Limpiar();
    }

    public void Comenzar(Vector2 superficie, Vector2 haciaSala, float segundos)
    {
        if (!isActiveAndEnabled || rocas == null || rocas.hojaEfectos == null || rocas.prefabRoca == null)
            return;
        efectos.RemoveAll(efecto => efecto == null);
        Vector3 punto = new Vector3(superficie.x, superficie.y, gusano.transform.position.z);
        GameObject polvo = EfectoRocaJefe.Crear(rocas.hojaEfectos, 1, punto,
            gusano.DiametroCuerpo * tamanoRelativo, rocas.prefabRoca.Imagen,
            Mathf.Max(0.3f, segundos), true);
        if (polvo != null)
        {
            polvo.transform.rotation = Quaternion.FromToRotation(Vector3.down, haciaSala);
            UtilidadAtaquesJefe.Ordenar(polvo.GetComponent<SpriteRenderer>(), sortingLayerPolvo, ordenPolvo);
            efectos.Add(polvo);
        }
        if (rocas.controlCamara != null)
            rocas.controlCamara.Sacudir(0.3f, 0.06f);
        AudioJuegoSimple.Instancia.Derrumbe();
    }

    private void PuffFinal()
    {
        if (rocas == null || rocas.hojaEfectos == null || rocas.prefabRoca == null)
            return;
        Vector2 lugar = gusano.PuntoEntierro;
        GameObject puff = EfectoRocaJefe.Crear(rocas.hojaEfectos, 2,
            new Vector3(lugar.x, lugar.y, gusano.transform.position.z),
            gusano.DiametroCuerpo * tamanoRelativo, rocas.prefabRoca.Imagen, 0.4f, false);
        if (puff != null)
        {
            UtilidadAtaquesJefe.Ordenar(puff.GetComponent<SpriteRenderer>(), sortingLayerPolvo, ordenPolvo + 1);
            efectos.Add(puff);
        }
    }
    public void Limpiar()
    {
        foreach (GameObject efecto in efectos)
            if (efecto != null)
                Destroy(efecto);
        efectos.Clear();
    }
}
