using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class EfectoExcavacionTaladro : MonoBehaviour
{
    [Header("Grietas y vibracion de cada celda")]
    [Tooltip("Etapas leve, media y profunda. No necesitan Animator.")]
    public Sprite[] grietas;
    public Color colorGrietas = Color.white;
    public bool mostrarTemblor = true;
    [Range(0f, 2f)] public float intensidadTemblorPixeles = 1f;
    [Min(1f)] public float frecuenciaTemblor = 18f;
    [Tooltip("Las grietas lejanas se ocultan, pero el dano se conserva al volver.")]
    [Min(1f)] public float distanciaVisualMaxima = 18f;
    [Tooltip("Normalmente se mantiene el orden del Tilemap.")]
    public int ordenRocasDanadas = 0;

    [Header("Animacion de contacto anterior, opcional")]
    [Tooltip("Desactivada para usar solo las particulas de polvo.")]
    public bool usarAnimacionContacto = false;
    public Sprite[] cuadrosContacto;
    [Min(1f)] public float cuadrosPorSegundo = 16f;
    [Min(0.01f)] public float anchoAnimacion = 1.2f;
    public Color colorContacto = Color.white;
    [Header("Orden de dibujo")]
    [Tooltip("Opcional: un SpriteRenderer del chasis para ordenar las particulas delante.")]
    public Renderer referenciaOrden;
    public int ordenParticulas = 5;

    [Header("Polvo pixel art")]
    public bool emitirPolvo = true;
    [FormerlySerializedAs("maximoFragmentos")]
    [Range(4, 128)] public int maximoParticulasPolvo = 48;
    [FormerlySerializedAs("velocidadFragmentos")]
    [Min(0.01f)] public float velocidadPolvo = 1.1f;
    [Tooltip("Los 8 cuadros de polvo ordenados. Cada particula los reproduce una sola vez.")]
    public Sprite[] cuadrosPolvo;
    [Min(0.02f)] public float intervaloPolvo = 0.14f;
    public Color colorPolvo = new Color(0.80f, 0.64f, 0.45f, 0.65f);
    [Min(0.01f)] public float tamanoPolvo = 0.60f;
    [Min(0.01f)] public float duracionPolvo = 0.50f;
    [Range(0, 6)] public int polvoAlRomper = 2;
    [Tooltip("Opcional: sistema de polvo que ya tuvieras. Puede quedar vacio.")]
    public ParticleSystem polvo;

    [Header("Sonido de contacto, opcional")]
    public AudioClip sonidoExcavar;
    [Range(0f, 1f)] public float volumen = 0.2f;

    private class ParticulaPolvo
    {
        public SpriteRenderer imagen;
        public Vector2 velocidad;
        public float restante;
        public float duracion;
        public bool polvoAnimado;
        public Color color;
        public Vector3 escalaInicial;
    }

    private class VisualCelda
    {
        public Tilemap mapa;
        public TilemapRenderer referencia;
        public Vector3Int celda;
        public TileBase tile;
        public Color colorOriginal;
        public TileFlags flagsOriginales;
        public GameObject objeto;
        public Transform grafica;
        public SortingGroup grupo;
        public SpriteRenderer roca;
        public SpriteRenderer grieta;
        public SpriteMask mascara;
        public float progreso;
        public bool tocando;
        public float semilla;
    }

    private readonly Dictionary<Vector3Int, VisualCelda> rocas =
        new Dictionary<Vector3Int, VisualCelda>();
    private readonly List<Vector3Int> retirar = new List<Vector3Int>();

    private Tilemap mapaVisual;
    private ParticulaPolvo[] particulas;
    private GameObject contenedor;
    private AudioSource audioContacto;
    private SpriteRenderer imagenContacto;
    private Texture2D texturaPolvo;
    private Sprite spritePolvo;
    private TaladroDestructivo taladroReferencia;

    private bool enContacto;
    private bool direccionFijada;
    private int direccionSalida = 1;
    private Vector2 puntoContacto;
    private Renderer referenciaActual;
    private float siguientePolvo;
    private float ultimoContacto = -10f;
    private float faseAnimacion;
    private int siguienteIndice;

    private void Awake()
    {
        taladroReferencia = GetComponent<TaladroDestructivo>();
        contenedor = new GameObject("PolvoExcavacion");
        particulas = new ParticulaPolvo[Mathf.Clamp(maximoParticulasPolvo, 4, 128)];
        for (int i = 0; i < particulas.Length; i++)
        {
            SpriteRenderer imagen = CrearImagen("ParticulaPolvo", contenedor.transform);
            imagen.enabled = false;
            particulas[i] = new ParticulaPolvo { imagen = imagen };
        }

        imagenContacto = CrearImagen("AnimacionContactoExcavacion", contenedor.transform);
        imagenContacto.enabled = false;
        audioContacto = gameObject.AddComponent<AudioSource>();
        audioContacto.playOnAwake = false;
        audioContacto.loop = true;
        audioContacto.spatialBlend = 0f;

        // Un punto de color para motas de polvo: no necesita textura externa.
        texturaPolvo = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texturaPolvo.name = "PuntoPolvoExcavacion";
        texturaPolvo.filterMode = FilterMode.Point;
        texturaPolvo.wrapMode = TextureWrapMode.Clamp;
        texturaPolvo.SetPixel(0, 0, Color.white);
        texturaPolvo.Apply();
        spritePolvo = Sprite.Create(texturaPolvo, new Rect(0, 0, 1, 1),
            new Vector2(0.5f, 0.5f), 16f);
    }

    private SpriteRenderer CrearImagen(string nombre, Transform padre)
    {
        GameObject objeto = new GameObject(nombre);
        objeto.layer = gameObject.layer;
        objeto.transform.SetParent(padre, false);
        return objeto.AddComponent<SpriteRenderer>();
    }

    public void EstablecerDireccion(int nuevaDireccion)
    {
        direccionSalida = nuevaDireccion < 0 ? -1 : 1;
        direccionFijada = true;
    }

    // Se conservan estas firmas para las llamadas de TaladroDestructivo.
    public void EstablecerContacto(bool valor, Vector2 punto, Sprite roca, Renderer referencia)
    {
        EstablecerContacto(valor, punto, roca, referencia, Color.white);
    }

    public void EstablecerContacto(bool valor, Vector2 punto, Sprite roca,
        Renderer referencia, Color colorRoca)
    {
        bool nuevoContacto = valor && isActiveAndEnabled;
        if (nuevoContacto)
        {
            if (!enContacto)
            {
                siguientePolvo = 0f;
                if (Time.time - ultimoContacto > 0.12f)
                    faseAnimacion = 0f;
            }
            ultimoContacto = Time.time;
            if (!direccionFijada)
                direccionSalida = DireccionDelTaladro(punto);
        }
        enContacto = nuevoContacto;
        puntoContacto = punto;
        referenciaActual = referencia;
        if (!enContacto && imagenContacto != null)
            imagenContacto.enabled = false;

        if (polvo != null)
        {
            if (enContacto)
            {
                polvo.transform.position = new Vector3(punto.x, punto.y, transform.position.z);
                if (!polvo.isPlaying)
                    polvo.Play();
            }
            else
                polvo.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (audioContacto == null)
            return;
        audioContacto.volume = Mathf.Clamp01(volumen);
        if (enContacto && sonidoExcavar != null)
        {
            if (audioContacto.clip != sonidoExcavar)
            {
                audioContacto.Stop();
                audioContacto.clip = sonidoExcavar;
            }
            if (!audioContacto.isPlaying)
                audioContacto.Play();
        }
        else
            audioContacto.Stop();
    }

    public void MostrarRotura(Vector2 punto, Sprite roca, Renderer referencia)
    {
        MostrarRotura(punto, roca, referencia, Color.white);
    }

    public void MostrarRotura(Vector2 punto, Sprite roca, Renderer referencia, Color colorRoca)
    {
        if (!isActiveAndEnabled || particulas == null)
            return;
        if (!direccionFijada)
            direccionSalida = DireccionDelTaladro(punto);
        if (emitirPolvo)
            for (int i = 0; i < Mathf.Clamp(polvoAlRomper, 0, 6); i++)
                EmitirPolvo(punto, referencia);
    }

    private void Update()
    {
        ActualizarAnimacionContacto();
        if (particulas == null)
            return;
        if (enContacto && emitirPolvo && Time.time >= siguientePolvo)
        {
            EmitirPolvo(puntoContacto, referenciaActual);
            siguientePolvo = Time.time + Mathf.Max(0.02f, intervaloPolvo);
        }
        foreach (ParticulaPolvo particula in particulas)
        {
            if (particula.restante <= 0f)
                continue;
            particula.restante = Mathf.Max(0f, particula.restante - Time.deltaTime);
            Transform objeto = particula.imagen.transform;
            objeto.position += (Vector3)(particula.velocidad * Time.deltaTime);
            float restante = Mathf.Clamp01(particula.restante / particula.duracion);
            if (particula.polvoAnimado && cuadrosPolvo != null && cuadrosPolvo.Length > 0)
            {
                int indice = Mathf.Min(
                    Mathf.FloorToInt((1f - restante) * cuadrosPolvo.Length),
                    cuadrosPolvo.Length - 1);
                if (cuadrosPolvo[indice] != null)
                    particula.imagen.sprite = cuadrosPolvo[indice];
            }
            Color color = particula.color;
            color.a *= particula.polvoAnimado ? Mathf.Clamp01(restante * 4f) : restante;
            particula.imagen.color = color;
            if (!particula.polvoAnimado)
                objeto.localScale = particula.escalaInicial * Mathf.Lerp(1.6f, 1f, restante);
            particula.imagen.enabled = particula.restante > 0f;
        }
    }

    private void EmitirPolvo(Vector2 punto, Renderer referencia)
    {
        if (particulas == null || particulas.Length == 0)
            return;
        bool animado = cuadrosPolvo != null &&
                       cuadrosPolvo.Length > 0 && cuadrosPolvo[0] != null;
        Sprite sprite = animado ? cuadrosPolvo[0] : spritePolvo;
        if (sprite == null)
            return;
        ParticulaPolvo particula = particulas[siguienteIndice];
        siguienteIndice = (siguienteIndice + 1) % particulas.Length;
        particula.imagen.sprite = sprite;
        particula.polvoAnimado = animado;
        particula.imagen.flipX = direccionSalida < 0;
        particula.color = colorPolvo;
        particula.imagen.color = particula.color;
        OrdenarParticula(particula.imagen, referencia);

        Vector2 variacion = new Vector2(-direccionSalida * Random.Range(0.01f, 0.04f),
            Random.Range(-0.025f, 0.025f));
        particula.imagen.transform.position =
            new Vector3(punto.x + variacion.x, punto.y + variacion.y, transform.position.z);
        // Usa la celda completa para que todos los cuadros tengan la misma escala.
        float lado = animado
            ? Mathf.Max(0.001f, Mathf.Max(sprite.rect.width, sprite.rect.height) / sprite.pixelsPerUnit)
            : Mathf.Max(0.001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
        float escala = Mathf.Max(0.01f, tamanoPolvo) * Random.Range(0.7f, 1.3f) / lado;
        particula.escalaInicial = Vector3.one * escala;
        particula.imagen.transform.localScale = particula.escalaInicial;
        particula.imagen.transform.rotation = Quaternion.identity;

        float velocidad = Mathf.Max(0.01f, velocidadPolvo);
        particula.velocidad = new Vector2(
            -direccionSalida * velocidad * Random.Range(0.1f, 0.25f),
            velocidad * Random.Range(0.05f, 0.2f));
        particula.duracion = Mathf.Max(0.01f, duracionPolvo);
        particula.restante = particula.duracion;
        particula.imagen.enabled = true;
    }

    private void OrdenarParticula(SpriteRenderer imagen, Renderer referencia)
    {
        Renderer orden = referenciaOrden != null ? referenciaOrden : referencia;
        if (orden != null)
        {
            imagen.sortingLayerID = orden.sortingLayerID;
            imagen.sortingOrder = Mathf.Clamp(orden.sortingOrder + ordenParticulas, -32768, 32767);
        }
        if (referencia != null && referencia.sharedMaterial != null)
            imagen.sharedMaterial = referencia.sharedMaterial;
    }

    private void ActualizarAnimacionContacto()
    {
        if (imagenContacto == null)
            return;
        if (!usarAnimacionContacto || !enContacto ||
            cuadrosContacto == null || cuadrosContacto.Length == 0)
        {
            imagenContacto.enabled = false;
            return;
        }
        faseAnimacion = Mathf.Repeat(
            faseAnimacion + Time.deltaTime * Mathf.Max(1f, cuadrosPorSegundo),
            cuadrosContacto.Length);
        Sprite cuadro = cuadrosContacto[Mathf.FloorToInt(faseAnimacion)];
        imagenContacto.sprite = cuadro;
        imagenContacto.color = colorContacto;
        imagenContacto.enabled = cuadro != null;
        if (cuadro == null)
            return;
        imagenContacto.transform.position =
            new Vector3(puntoContacto.x, puntoContacto.y, transform.position.z);
        imagenContacto.transform.rotation = Quaternion.identity;
        imagenContacto.flipX = direccionSalida < 0;
        imagenContacto.transform.localScale =
            Vector3.one * (Mathf.Max(0.01f, anchoAnimacion) / Mathf.Max(0.001f, cuadro.bounds.size.x));
        OrdenarParticula(imagenContacto, referenciaActual);
    }

    public Color ObtenerColorRoca(Tilemap mapa, Vector3Int celda)
    {
        if (mapa == null)
            return Color.white;
        if (mapaVisual == mapa && rocas.TryGetValue(celda, out VisualCelda visual) &&
            mapa.GetTile(celda) == visual.tile)
            return visual.colorOriginal * mapa.color;
        return mapa.GetColor(celda) * mapa.color;
    }

    public void ActualizarRoca(Tilemap mapa, Vector3Int celda, float progreso, bool tocando)
    {
        if (!isActiveAndEnabled || mapa == null || mapa.GetSprite(celda) == null)
            return;
        if (mapaVisual != mapa)
        {
            LimpiarRocas();
            mapaVisual = mapa;
        }
        float alcance = Mathf.Max(1f, distanciaVisualMaxima);
        if ((mapa.GetCellCenterWorld(celda) - transform.position).sqrMagnitude > alcance * alcance)
        {
            RetirarRoca(celda);
            return;
        }
        if (rocas.TryGetValue(celda, out VisualCelda anterior) &&
            anterior.tile != mapa.GetTile(celda))
            RetirarRoca(celda);
        if (!rocas.TryGetValue(celda, out VisualCelda visual))
        {
            visual = CrearVisual(mapa, celda);
            rocas.Add(celda, visual);
        }
        visual.progreso = Mathf.Clamp01(progreso);
        visual.tocando = tocando;
    }

    private VisualCelda CrearVisual(Tilemap mapa, Vector3Int celda)
    {
        VisualCelda visual = new VisualCelda
        {
            mapa = mapa,
            celda = celda,
            tile = mapa.GetTile(celda),
            referencia = mapa.GetComponent<TilemapRenderer>(),
            colorOriginal = mapa.GetColor(celda),
            flagsOriginales = mapa.GetTileFlags(celda),
            semilla = Mathf.Repeat(celda.x * 13.37f + celda.y * 7.91f, 100f)
        };
        visual.objeto = new GameObject("RocaDanada_" + celda.x + "_" + celda.y);
        visual.objeto.layer = mapa.gameObject.layer;
        visual.objeto.transform.SetParent(mapa.transform, false);
        visual.grupo = visual.objeto.AddComponent<SortingGroup>();

        GameObject grafica = new GameObject("GraficaSinCollider");
        grafica.layer = mapa.gameObject.layer;
        grafica.transform.SetParent(visual.objeto.transform, false);
        visual.grafica = grafica.transform;
        visual.roca = CrearImagen("Roca", visual.grafica);
        visual.roca.gameObject.layer = mapa.gameObject.layer;
        visual.roca.maskInteraction = SpriteMaskInteraction.None;
        visual.roca.sortingOrder = 0;
        visual.grieta = CrearImagen("Grietas", visual.grafica);
        visual.grieta.gameObject.layer = mapa.gameObject.layer;
        visual.grieta.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        visual.grieta.sortingOrder = 1;
        visual.grieta.enabled = false;

        GameObject mascara = new GameObject("MascaraDeLaRoca");
        mascara.layer = mapa.gameObject.layer;
        mascara.transform.SetParent(visual.grafica, false);
        visual.mascara = mascara.AddComponent<SpriteMask>();
        visual.mascara.alphaCutoff = 0.1f;
        visual.mascara.isCustomRangeActive = false;
        return visual;
    }

    private void LateUpdate()
    {
        retirar.Clear();
        foreach (KeyValuePair<Vector3Int, VisualCelda> par in rocas)
        {
            VisualCelda visual = par.Value;
            if (visual.mapa == null || visual.objeto == null ||
                visual.mapa.GetTile(visual.celda) != visual.tile ||
                visual.mapa.GetSprite(visual.celda) == null)
            {
                retirar.Add(par.Key);
                continue;
            }
            ActualizarVisual(visual);
        }
        foreach (Vector3Int celda in retirar)
            RetirarRoca(celda);
    }

    private void ActualizarVisual(VisualCelda visual)
    {
        Tilemap mapa = visual.mapa;
        Sprite sprite = mapa.GetSprite(visual.celda);
        visual.roca.sprite = sprite;
        visual.mascara.sprite = sprite;
        visual.roca.color = visual.colorOriginal * mapa.color;
        if (visual.referencia != null)
        {
            int capa = visual.referencia.sortingLayerID;
            visual.grupo.sortingLayerID = capa;
            visual.grupo.sortingOrder =
                Mathf.Clamp(visual.referencia.sortingOrder + ordenRocasDanadas, -32768, 32767);
            visual.roca.sortingLayerID = capa;
            visual.grieta.sortingLayerID = capa;
            visual.roca.sharedMaterial = visual.referencia.sharedMaterial;
            visual.grieta.sharedMaterial = visual.referencia.sharedMaterial;
        }

        // Conserva giros y reflejos del Rule Tile sin mover el collider.
        Matrix4x4 matriz = mapa.orientationMatrix * mapa.GetTransformMatrix(visual.celda);
        Vector3 posicion = mapa.CellToLocalInterpolated(
            (Vector3)visual.celda + mapa.tileAnchor) + matriz.MultiplyPoint3x4(Vector3.zero);
        float escalaX = Mathf.Sqrt(matriz.m00 * matriz.m00 + matriz.m10 * matriz.m10);
        float escalaY = Mathf.Sqrt(matriz.m01 * matriz.m01 + matriz.m11 * matriz.m11);
        float determinante = matriz.m00 * matriz.m11 - matriz.m01 * matriz.m10;
        visual.objeto.transform.localPosition = posicion;
        visual.objeto.transform.localRotation =
            Quaternion.Euler(0f, 0f, Mathf.Atan2(matriz.m10, matriz.m00) * Mathf.Rad2Deg);
        visual.objeto.transform.localScale =
            new Vector3(escalaX, determinante < 0f ? -escalaY : escalaY, 1f);

        Vector3 vibracion = Vector3.zero;
        if (mostrarTemblor && visual.tocando && intensidadTemblorPixeles > 0f)
        {
            // Desplazamientos enteros de pixel; la frecuencia controla su cambio.
            float paso = Mathf.Floor(Time.time * Mathf.Max(1f, frecuenciaTemblor));
            float amplitud = Mathf.Clamp(intensidadTemblorPixeles, 0f, 2f) *
                             Mathf.Lerp(0.6f, 1f, visual.progreso);
            float x = Mathf.Round(Mathf.Sin(paso * 2.17f + visual.semilla) * amplitud);
            float y = Mathf.Round(Mathf.Sin(paso * 1.71f + visual.semilla * 2f) * amplitud);
            vibracion = new Vector3(x, y, 0f) / Mathf.Max(1f, sprite.pixelsPerUnit);
        }
        visual.grafica.localPosition = vibracion;

        bool hayGrietas = grietas != null && grietas.Length > 0 && visual.progreso > 0f;
        Sprite grieta = hayGrietas
            ? grietas[Mathf.Min(Mathf.FloorToInt(visual.progreso * grietas.Length), grietas.Length - 1)]
            : null;
        visual.grieta.sprite = grieta;
        visual.grieta.enabled = grieta != null;
        visual.grieta.color = colorGrietas * mapa.color;
        if (grieta != null)
        {
            float ancho = sprite.rect.width / sprite.pixelsPerUnit;
            float alto = sprite.rect.height / sprite.pixelsPerUnit;
            visual.grieta.transform.localPosition = new Vector3(
                (sprite.rect.width * 0.5f - sprite.pivot.x) / sprite.pixelsPerUnit,
                (sprite.rect.height * 0.5f - sprite.pivot.y) / sprite.pixelsPerUnit, 0f);
            visual.grieta.transform.localScale = new Vector3(
                ancho / Mathf.Max(0.001f, grieta.rect.width / grieta.pixelsPerUnit),
                alto / Mathf.Max(0.001f, grieta.rect.height / grieta.pixelsPerUnit), 1f);
        }

        // Solo oculta la imagen original; el tile y su colision siguen intactos.
        // Se reaplica tras refrescar vecinos de un Rule Tile.
        mapa.RemoveTileFlags(visual.celda, TileFlags.LockColor);
        Color oculto = visual.colorOriginal;
        oculto.a = 0f;
        mapa.SetColor(visual.celda, oculto);
    }

    public void DetenerTemblor()
    {
        foreach (VisualCelda visual in rocas.Values)
        {
            visual.tocando = false;
            if (visual.grafica != null)
                visual.grafica.localPosition = Vector3.zero;
        }
    }

    public void RetirarRoca(Vector3Int celda)
    {
        if (!rocas.TryGetValue(celda, out VisualCelda visual))
            return;
        RestaurarRoca(visual);
        rocas.Remove(celda);
    }

    private void RestaurarRoca(VisualCelda visual)
    {
        if (visual.mapa != null && visual.mapa.GetTile(visual.celda) == visual.tile)
        {
            visual.mapa.RemoveTileFlags(visual.celda, TileFlags.LockColor);
            visual.mapa.SetColor(visual.celda, visual.colorOriginal);
            visual.mapa.SetTileFlags(visual.celda, visual.flagsOriginales);
        }
        if (visual.objeto != null)
        {
            visual.objeto.SetActive(false);
            Destroy(visual.objeto);
        }
    }

    public void LimpiarRocas()
    {
        foreach (VisualCelda visual in rocas.Values)
            RestaurarRoca(visual);
        rocas.Clear();
        mapaVisual = null;
    }

    private int DireccionDelTaladro(Vector2 punto)
    {
        if (taladroReferencia != null && taladroReferencia.colliderChasis != null)
            return punto.x >= taladroReferencia.colliderChasis.bounds.center.x ? 1 : -1;
        return transform.lossyScale.x < 0f ? -1 : 1;
    }

    private void OnDisable()
    {
        EstablecerContacto(false, Vector2.zero, null, null);
        LimpiarRocas();
        if (particulas != null)
            foreach (ParticulaPolvo particula in particulas)
            {
                particula.restante = 0f;
                if (particula.imagen != null)
                    particula.imagen.enabled = false;
            }
    }

    private void OnDestroy()
    {
        LimpiarRocas();
        if (contenedor != null)
            Destroy(contenedor);
        if (audioContacto != null)
            Destroy(audioContacto);
        if (spritePolvo != null)
            Destroy(spritePolvo);
        if (texturaPolvo != null)
            Destroy(texturaPolvo);
    }
}

