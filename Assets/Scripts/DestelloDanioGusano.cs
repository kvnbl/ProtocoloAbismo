using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DestelloDanioGusano : MonoBehaviour
{
    public Shader shaderDestello;
    [Min(0.02f)] public float duracion = 0.12f;
    [Range(0f, 1f)] public float intensidad = 0.65f;
    private readonly List<SpriteRenderer> imagenes = new List<SpriteRenderer>();
    private readonly List<Material> originales = new List<Material>();
    private Material material;
    private float restante;
    private bool aplicado;
    private bool aviso;

    public void Mostrar()
    {
        if (!isActiveAndEnabled)
            return;
        if (material == null)
        {
            Shader shader = shaderDestello != null ? shaderDestello : Shader.Find("Sprites/DestelloGusano");
            if (shader == null || !shader.isSupported)
            {
                if (!aviso)
                    Debug.LogWarning("Asigna DestelloGusano.shader en DestelloDanioGusano.", this);
                aviso = true;
                return;
            }
            material = new Material(shader);
        }
        if (!aplicado)
        {
            imagenes.Clear();
            originales.Clear();
            foreach (SpriteRenderer imagen in GetComponentsInChildren<SpriteRenderer>(true))
            {
                imagenes.Add(imagen);
                originales.Add(imagen.sharedMaterial);
                imagen.sharedMaterial = material;
            }
            aplicado = true;
        }
        restante = Mathf.Max(0.02f, duracion);
        material.SetFloat("_Destello", intensidad);
    }

    private void LateUpdate()
    {
        if (!aplicado)
            return;
        restante -= Time.deltaTime;
        if (restante <= 0f)
        {
            Restaurar();
            return;
        }
        material.SetFloat("_Destello", intensidad * Mathf.Clamp01(restante / Mathf.Max(0.02f, duracion)));
    }

    private void Restaurar()
    {
        for (int i = 0; i < imagenes.Count; i++)
            if (imagenes[i] != null)
                imagenes[i].sharedMaterial = originales[i];
        aplicado = false;
        restante = 0f;
    }

    private void OnDisable() => Restaurar();
    private void OnDestroy()
    {
        Restaurar();
        if (material != null)
            Destroy(material);
    }
}
