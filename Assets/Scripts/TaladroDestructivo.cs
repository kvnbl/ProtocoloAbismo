using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class TaladroDestructivo : MonoBehaviour
{
    [Header("Rocas que puede romper")]
    public Tilemap rocasDestructibles;

    [Header("Tiempo de excavacion")]
    [Min(0.01f)] public float intervalo = 0.12f;
    [Tooltip("Contacto acumulado necesario para romper CADA celda.")]
    [Min(0.01f)] public float resistenciaRoca = 0.30f;
    [Range(1, 8)] public int celdasPorRotura = 1;
    [Range(1, 32)] public int maximoCeldasEnContacto = 8;

    [Header("Forma del corte")]
    [Tooltip("Collider SOLIDO del chasis completo. No la punta ni un detector de suelo pequeno.")]
    public Collider2D colliderChasis;
    [Tooltip("Ensanche en la base para abrir paso al chasis; la punta sigue siendo estrecha.")]
    public bool asegurarPasoChasis = true;
    [Range(0.05f, 1f)] public float grosorRelativoPunta = 0.35f;
    [Min(0f)] public float margenSuperior = 0.08f;
    [Min(0.001f)] public float margenSuelo = 0.01f;
    [Min(0f)] public float solapeFrontal = 0.05f;

    // Se conserva para no perder compatibilidad con escenas antiguas.
    // El corte nuevo ya no elimina columnas completas.
    [HideInInspector] public int columnasPorCorte = 1;

    [Header("Freno durante la excavacion")]
    [Tooltip("Reduce el avance antes de chocar con la roca mientras perforas.")]
    public bool frenarAntesDeRoca = true;
    [Min(0.001f)] public float margenFrenado = 0.025f;

    [Header("Efecto de excavacion")]
    public EfectoExcavacionTaladro efectoExcavacion;

    [Header("Fondo que queda al excavar, opcional")]
    public Tilemap fondoExcavado;
    public TileBase tileFondo;

    private class DanoCelda
    {
        public TileBase tile;
        public float progreso;
        public bool tocando;
    }

    private readonly Dictionary<Vector3Int, DanoCelda> danos =
        new Dictionary<Vector3Int, DanoCelda>();
    private readonly List<Vector3Int> contactos = new List<Vector3Int>(32);
    private readonly List<Vector3Int> retirar = new List<Vector3Int>(32);
    private readonly List<Vector3Int> rotas = new List<Vector3Int>(8);
    private readonly Vector2[] forma = new Vector2[4];

    private readonly List<Collider2D> collidersAvance = new List<Collider2D>(8);
    private readonly RaycastHit2D[] resultadosAvance = new RaycastHit2D[32];
    private readonly ContactPoint2D[] contactosAvance = new ContactPoint2D[32];

    private BoxCollider2D zona;
    private Tilemap mapaAnterior;
    private TilemapCollider2D colliderRocas;
    private TilemapRenderer rendererRocas;
    private bool activo;
    private bool avisoConfiguracion;
    private int direccion = 1;
    private float proximaPerforacion;
    private const float Epsilon = 0.0001f;

    private void Awake()
    {
        zona = GetComponent<BoxCollider2D>();
        zona.isTrigger = true;
        if (efectoExcavacion == null)
            efectoExcavacion = GetComponent<EfectoExcavacionTaladro>();
        if (colliderChasis == null)
        {
            ExcavadoraController jugador = GetComponentInParent<ExcavadoraController>();
            if (jugador != null)
                colliderChasis = jugador.colliderSuelo;
        }
    }

    public void EstablecerActivo(bool nuevoEstado)
    {
        if (nuevoEstado && !activo)
            proximaPerforacion = 0f;
        activo = nuevoEstado;
        if (!activo)
            DetenerContacto();
    }


    // Consulta previa al avance. No mueve el cuerpo ni elimina colisiones.
    public bool IntentarFrenarAvance(Rigidbody2D cuerpo, float entradaX,
        float fuerzaHorizontal, out float velocidadX)
    {
        velocidadX = cuerpo != null ? cuerpo.linearVelocity.x : 0f;
        if (!frenarAntesDeRoca || !activo || !isActiveAndEnabled ||
            cuerpo == null || colliderChasis == null || zona == null ||
            !zona.enabled || rocasDestructibles == null ||
            Mathf.Abs(entradaX) < 0.001f)
            return false;

        PrepararReferencias();
        if (colliderRocas == null || !colliderRocas.enabled ||
            !colliderRocas.gameObject.activeInHierarchy)
            return false;
        Collider2D terreno = colliderRocas.composite != null
            ? (Collider2D)colliderRocas.composite : colliderRocas;
        if (!terreno.enabled || terreno.isTrigger)
            return false;

        int sentido = zona.bounds.center.x >= colliderChasis.bounds.center.x ? 1 : -1;
        if (entradaX * sentido <= 0f)
            return false;
        float paso = Mathf.Max(0.0001f, Time.fixedDeltaTime);
        float prevista = cuerpo.linearVelocity.x +
            fuerzaHorizontal * paso / Mathf.Max(0.0001f, cuerpo.mass);
        float haciaRoca = prevista * sentido;
        if (haciaRoca <= 0f)
            return false;

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask((1 << colliderRocas.gameObject.layer) |
                           (1 << terreno.gameObject.layer));
        filtro.useTriggers = false;

        // Un contacto lateral real evita seguir aplicando fuerza contra la pared.
        int cantidad = cuerpo.GetContacts(filtro, contactosAvance);
        for (int i = 0; i < cantidad; i++)
        {
            ContactPoint2D contacto = contactosAvance[i];
            if (!contacto.enabled || contacto.normal.x * sentido > -0.65f ||
                Mathf.Abs(contacto.normal.y) > 0.5f)
                continue;
            if (EsSuperficieDestructible(contacto.collider, contacto.point, contacto.normal) ||
                EsSuperficieDestructible(contacto.otherCollider, contacto.point, contacto.normal))
            {
                velocidadX = 0f;
                return true;
            }
        }

        float margen = Mathf.Max(Mathf.Max(0.001f, margenFrenado),
                                  Physics2D.defaultContactOffset * 2f);
        float distancia = haciaRoca * paso + margen;
        float permitida = haciaRoca;
        bool hayLimite = false;
        cuerpo.GetComponentsInChildren<Collider2D>(false, collidersAvance);
        foreach (Collider2D solido in collidersAvance)
        {
            if (solido == null || !solido.enabled || solido.isTrigger ||
                !solido.gameObject.activeInHierarchy || solido.attachedRigidbody != cuerpo ||
                !PuedeColisionar(solido, terreno))
                continue;
            cantidad = solido.Cast(Vector2.right * sentido, filtro,
                resultadosAvance, distancia, true);
            for (int i = 0; i < cantidad; i++)
            {
                RaycastHit2D golpe = resultadosAvance[i];
                // Un solapamiento inicial puede devolver una normal artificial.
                // Los contactos reales de arriba se encargan de ese caso.
                if (golpe.distance <= 0f || golpe.normal.x * sentido > -0.65f ||
                    Mathf.Abs(golpe.normal.y) > 0.5f ||
                    !EsSuperficieDestructible(golpe.collider, golpe.point, golpe.normal))
                    continue;
                float maxima = CalcularAvancePermitido(haciaRoca, golpe.distance, margen, paso);
                if (maxima < permitida)
                {
                    permitida = maxima;
                    hayLimite = true;
                }
            }
        }
        if (!hayLimite)
            return false;
        velocidadX = permitida * sentido;
        return true;
    }

    private static float CalcularAvancePermitido(float velocidadHaciaRoca,
        float distancia, float margen, float paso)
    {
        float hueco = Mathf.Max(0f, distancia - margen);
        float maxima = hueco / Mathf.Max(0.0001f, paso);
        return Mathf.Min(velocidadHaciaRoca, maxima);
    }

    private static bool PuedeColisionar(Collider2D a, Collider2D b)
    {
        if (Physics2D.GetIgnoreCollision(a, b))
            return false;
        bool matriz = !Physics2D.GetIgnoreLayerCollision(a.gameObject.layer, b.gameObject.layer);
        DecisionCapas(a, b.gameObject.layer, matriz, out bool usaA, out bool permiteA);
        DecisionCapas(b, a.gameObject.layer, matriz, out bool usaB, out bool permiteB);
        if (permiteA == permiteB)
            return permiteA;
        if (!usaA)
            return permiteB;
        if (!usaB)
            return permiteA;
        if (a.layerOverridePriority == b.layerOverridePriority)
            return false;
        return a.layerOverridePriority > b.layerOverridePriority ? permiteA : permiteB;
    }

    private static void DecisionCapas(Collider2D col, int capa, bool matriz,
        out bool usaOverride, out bool permite)
    {
        int incluir = col.includeLayers.value;
        int excluir = col.excludeLayers.value;
        Rigidbody2D cuerpo = col.attachedRigidbody;
        if (cuerpo != null)
        {
            incluir |= cuerpo.includeLayers.value;
            excluir |= cuerpo.excludeLayers.value;
        }
        int bit = 1 << capa;
        usaOverride = ((incluir | excluir) & bit) != 0;
        permite = (excluir & bit) != 0 ? false : (incluir & bit) != 0 ? true : matriz;
    }

    private bool EsSuperficieDestructible(Collider2D otro, Vector2 punto, Vector2 normal)
    {
        if (otro == null || colliderRocas == null || rocasDestructibles == null)
            return false;
        Collider2D compuesto = colliderRocas.composite;
        if (otro != colliderRocas && otro != compuesto)
            return false;
        // Un Composite puede reunir varios Tilemaps. Comprueba la celda concreta,
        // y descarta tambien los contactos antiguos de una celda que ya se rompio.
        Vector3 pasoX = rocasDestructibles.CellToWorld(Vector3Int.right) -
                        rocasDestructibles.CellToWorld(Vector3Int.zero);
        Vector3 pasoY = rocasDestructibles.CellToWorld(Vector3Int.up) -
                        rocasDestructibles.CellToWorld(Vector3Int.zero);
        float muestra = Mathf.Max(0.0001f,
            Mathf.Min(pasoX.magnitude, pasoY.magnitude) * 0.02f);
        Vector2 interior = punto - normal * muestra;
        Vector3Int celda = rocasDestructibles.WorldToCell(
            new Vector3(interior.x, interior.y, rocasDestructibles.transform.position.z));
        return rocasDestructibles.HasTile(celda);
    }


    private void FixedUpdate()
    {
        PrepararReferencias();
        foreach (DanoCelda dano in danos.Values)
            dano.tocando = false;

        contactos.Clear();
        rotas.Clear();
        bool puedeExcavar = activo && ConfiguracionValida();
        if (puedeExcavar)
        {
            Bounds limites = ConstruirForma();
            BuscarCeldas(limites);
        }

        if (contactos.Count == 0)
        {
            DetenerContacto();
            ActualizarVisuales();
            return;
        }

        Vector3Int principal = contactos[0];
        Vector2 punto = PuntoDeContacto(principal);
        if (efectoExcavacion != null)
        {
            efectoExcavacion.EstablecerDireccion(direccion);
            efectoExcavacion.EstablecerContacto(
                true, punto, rocasDestructibles.GetSprite(principal),
                rendererRocas, ColorDeRoca(principal));
        }

        float aumento = Time.fixedDeltaTime / Mathf.Max(0.01f, resistenciaRoca);
        foreach (Vector3Int celda in contactos)
        {
            TileBase tile = rocasDestructibles.GetTile(celda);
            if (tile == null)
                continue;
            if (!danos.TryGetValue(celda, out DanoCelda dano) || dano.tile != tile)
            {
                if (efectoExcavacion != null)
                    efectoExcavacion.RetirarRoca(celda);
                dano = new DanoCelda { tile = tile };
                danos[celda] = dano;
            }
            dano.tocando = true;
            dano.progreso = Mathf.Min(1f, dano.progreso + aumento);
        }

        if (Time.time >= proximaPerforacion)
        {
            foreach (Vector3Int celda in contactos)
            {
                if (rotas.Count >= Mathf.Clamp(celdasPorRotura, 1, 8))
                    break;
                if (danos.TryGetValue(celda, out DanoCelda dano) && dano.progreso >= 1f)
                    RomperCelda(celda);
            }
            if (rotas.Count > 0)
            {
                proximaPerforacion = Time.time + Mathf.Max(0.01f, intervalo);
                ActualizarTerreno();
            }
        }
        ActualizarVisuales();
    }

    private void PrepararReferencias()
    {
        if (mapaAnterior == rocasDestructibles)
            return;
        if (efectoExcavacion != null)
            efectoExcavacion.LimpiarRocas();
        danos.Clear();
        DetenerContacto();
        mapaAnterior = rocasDestructibles;
        colliderRocas = rocasDestructibles != null
            ? rocasDestructibles.GetComponent<TilemapCollider2D>() : null;
        rendererRocas = rocasDestructibles != null
            ? rocasDestructibles.GetComponent<TilemapRenderer>() : null;
        avisoConfiguracion = false;
    }

    private bool ConfiguracionValida()
    {
        if (rocasDestructibles == null || zona == null || !zona.enabled ||
            !zona.gameObject.activeInHierarchy || colliderChasis == null ||
            !colliderChasis.enabled || colliderChasis.isTrigger ||
            !colliderChasis.gameObject.activeInHierarchy)
            return false;

        // Esta version trabaja en un Grid rectangular XY, sin rotacion ni separacion.
        GridLayout grid = rocasDestructibles.layoutGrid;
        Vector3 pasoX = rocasDestructibles.CellToWorld(Vector3Int.right) -
                        rocasDestructibles.CellToWorld(Vector3Int.zero);
        Vector3 pasoY = rocasDestructibles.CellToWorld(Vector3Int.up) -
                        rocasDestructibles.CellToWorld(Vector3Int.zero);
        bool valido = grid != null && grid.cellLayout == GridLayout.CellLayout.Rectangle &&
                      pasoX.x > Epsilon && pasoY.y > Epsilon &&
                      Mathf.Abs(pasoX.y) < Epsilon && Mathf.Abs(pasoY.x) < Epsilon &&
                      Mathf.Abs(pasoX.z) < Epsilon && Mathf.Abs(pasoY.z) < Epsilon;
        Grid gridRectangular = grid as Grid;
        if (gridRectangular != null && gridRectangular.cellGap.sqrMagnitude > Epsilon * Epsilon)
            valido = false;
        if (!valido && !avisoConfiguracion)
        {
            avisoConfiguracion = true;
            Debug.LogWarning(
                "TaladroDestructivo necesita un Grid rectangular XY, sin rotacion, " +
                "escala negativa ni Cell Gap.", this);
        }
        return valido;
    }

    private Bounds ConstruirForma()
    {
        Bounds punta = zona.bounds;
        Bounds chasis = colliderChasis.bounds;
        direccion = punta.center.x >= chasis.center.x ? 1 : -1;
        float baseX = direccion > 0 ? punta.min.x : punta.max.x;
        float frenteX = direccion > 0 ? punta.max.x : punta.min.x;
        float inferior = punta.min.y;
        float superior = punta.max.y;

        if (asegurarPasoChasis)
        {
            baseX = direccion > 0
                ? Mathf.Min(baseX, chasis.max.x - Mathf.Max(0f, solapeFrontal))
                : Mathf.Max(baseX, chasis.min.x + Mathf.Max(0f, solapeFrontal));
            inferior = Mathf.Min(inferior, chasis.min.y + Mathf.Max(0.001f, margenSuelo));
            superior = Mathf.Max(superior, chasis.max.y + Mathf.Max(0f, margenSuperior));
        }

        float mitadPunta = punta.extents.y * Mathf.Clamp(grosorRelativoPunta, 0.05f, 1f);
        forma[0] = new Vector2(baseX, inferior);
        forma[1] = new Vector2(frenteX, punta.center.y - mitadPunta);
        forma[2] = new Vector2(frenteX, punta.center.y + mitadPunta);
        forma[3] = new Vector2(baseX, superior);

        Bounds limites = new Bounds(
            new Vector3(forma[0].x, forma[0].y, rocasDestructibles.transform.position.z),
            Vector3.zero);
        for (int i = 1; i < forma.Length; i++)
            limites.Encapsulate(new Vector3(forma[i].x, forma[i].y, limites.center.z));
        return limites;
    }

    private void BuscarCeldas(Bounds limites)
    {
        Vector3Int minimo = rocasDestructibles.WorldToCell(
            limites.min + new Vector3(Epsilon, Epsilon, 0f));
        Vector3Int maximo = rocasDestructibles.WorldToCell(
            limites.max - new Vector3(Epsilon, Epsilon, 0f));
        long cantidad = ((long)maximo.x - minimo.x + 1) *
                        ((long)maximo.y - minimo.y + 1);
        if (cantidad <= 0 || cantidad > 4096)
            return;

        int inicio = direccion > 0 ? minimo.x : maximo.x;
        int fin = direccion > 0 ? maximo.x : minimo.x;
        for (int y = minimo.y; y <= maximo.y; y++)
        {
            // Solo la primera celda alcanzada en cada fila: no atraviesa una pared.
            for (int x = inicio; direccion > 0 ? x <= fin : x >= fin; x += direccion)
            {
                Vector3Int celda = new Vector3Int(x, y, minimo.z);
                if (!rocasDestructibles.HasTile(celda) || !IntersecaForma(LimitesCelda(celda)))
                    continue;
                contactos.Add(celda);
                break;
            }
        }
        contactos.Sort(CompararCeldas);
        int maximoContacto = Mathf.Clamp(maximoCeldasEnContacto, 1, 32);
        if (contactos.Count > maximoContacto)
            contactos.RemoveRange(maximoContacto, contactos.Count - maximoContacto);
    }

    private int CompararCeldas(Vector3Int a, Vector3Int b)
    {
        // Prioridad al frente mas cercano al chasis; despues al centro del taladro.
        int horizontal = direccion > 0 ? a.x.CompareTo(b.x) : b.x.CompareTo(a.x);
        if (horizontal != 0)
            return horizontal;
        float centroY = zona.bounds.center.y;
        float distanciaA = Mathf.Abs(rocasDestructibles.GetCellCenterWorld(a).y - centroY);
        float distanciaB = Mathf.Abs(rocasDestructibles.GetCellCenterWorld(b).y - centroY);
        int vertical = distanciaA.CompareTo(distanciaB);
        return vertical != 0 ? vertical : a.y.CompareTo(b.y);
    }

    private Bounds LimitesCelda(Vector3Int celda)
    {
        Vector3 a = rocasDestructibles.CellToWorld(celda);
        Vector3 b = rocasDestructibles.CellToWorld(celda + new Vector3Int(1, 1, 0));
        return new Bounds((a + b) * 0.5f,
            new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
    }

    private bool IntersecaForma(Bounds celda)
    {
        if (Separados(celda, Vector2.right) || Separados(celda, Vector2.up))
            return false;
        for (int i = 0; i < forma.Length; i++)
        {
            Vector2 lado = forma[(i + 1) % forma.Length] - forma[i];
            Vector2 normal = new Vector2(-lado.y, lado.x);
            if (normal.sqrMagnitude > Epsilon * Epsilon &&
                Separados(celda, normal.normalized))
                return false;
        }
        return true;
    }

    private bool Separados(Bounds celda, Vector2 eje)
    {
        float minimo = Vector2.Dot(forma[0], eje);
        float maximo = minimo;
        for (int i = 1; i < forma.Length; i++)
        {
            float proyeccion = Vector2.Dot(forma[i], eje);
            minimo = Mathf.Min(minimo, proyeccion);
            maximo = Mathf.Max(maximo, proyeccion);
        }
        float centro = Vector2.Dot((Vector2)celda.center, eje);
        float radio = celda.extents.x * Mathf.Abs(eje.x) +
                      celda.extents.y * Mathf.Abs(eje.y);
        return maximo <= centro - radio + Epsilon ||
               minimo >= centro + radio - Epsilon;
    }

    private Vector2 PuntoDeContacto(Vector3Int celda)
    {
        Bounds limites = LimitesCelda(celda);
        return new Vector2(direccion > 0 ? limites.min.x : limites.max.x,
            Mathf.Clamp(zona.bounds.center.y, limites.min.y, limites.max.y));
    }

    private Color ColorDeRoca(Vector3Int celda)
    {
        return efectoExcavacion != null
            ? efectoExcavacion.ObtenerColorRoca(rocasDestructibles, celda)
            : rocasDestructibles.GetColor(celda) * rocasDestructibles.color;
    }

    private void RomperCelda(Vector3Int celda)
    {
        Vector2 punto = PuntoDeContacto(celda);
        Sprite sprite = rocasDestructibles.GetSprite(celda);
        Color color = ColorDeRoca(celda);
        if (efectoExcavacion != null)
        {
            efectoExcavacion.RetirarRoca(celda);
            efectoExcavacion.MostrarRotura(punto, sprite, rendererRocas, color);
        }
        rocasDestructibles.SetTile(celda, null);
        danos.Remove(celda);
        rotas.Add(celda);

        if (fondoExcavado != null && fondoExcavado != rocasDestructibles &&
            tileFondo != null && fondoExcavado.GetComponent<Collider2D>() == null)
        {
            Vector3Int fondo = fondoExcavado.WorldToCell(
                rocasDestructibles.GetCellCenterWorld(celda));
            if (!fondoExcavado.HasTile(fondo))
                fondoExcavado.SetTile(fondo, tileFondo);
        }
    }

    private void ActualizarTerreno()
    {
        foreach (Vector3Int celda in rotas)
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    rocasDestructibles.RefreshTile(celda + new Vector3Int(x, y, 0));

        if (colliderRocas != null && colliderRocas.enabled)
        {
            colliderRocas.ProcessTilemapChanges();
            CompositeCollider2D compuesto = colliderRocas.composite;
            if (compuesto != null && compuesto.enabled &&
                compuesto.generationType == CompositeCollider2D.GenerationType.Manual)
                compuesto.GenerateGeometry();
        }
    }

    private void ActualizarVisuales()
    {
        retirar.Clear();
        foreach (KeyValuePair<Vector3Int, DanoCelda> par in danos)
        {
            if (rocasDestructibles == null ||
                rocasDestructibles.GetTile(par.Key) != par.Value.tile)
            {
                retirar.Add(par.Key);
                continue;
            }
            if (efectoExcavacion != null)
                efectoExcavacion.ActualizarRoca(
                    rocasDestructibles, par.Key, par.Value.progreso, par.Value.tocando);
        }
        // Las retiradas se hacen despues del foreach.
        foreach (Vector3Int celda in retirar)
        {
            if (efectoExcavacion != null)
                efectoExcavacion.RetirarRoca(celda);
            danos.Remove(celda);
        }
    }

    private void DetenerContacto()
    {
        foreach (DanoCelda dano in danos.Values)
            dano.tocando = false;
        if (efectoExcavacion != null)
        {
            efectoExcavacion.EstablecerContacto(false, Vector2.zero, null, null);
            efectoExcavacion.DetenerTemblor();
        }
    }

    private void OnDisable()
    {
        activo = false;
        DetenerContacto();
    }

    private void OnDestroy()
    {
        if (efectoExcavacion != null)
            efectoExcavacion.LimpiarRocas();
    }

    private void OnDrawGizmosSelected()
    {
        if (zona == null)
            zona = GetComponent<BoxCollider2D>();
        if (zona == null || colliderChasis == null || rocasDestructibles == null)
            return;
        ConstruirForma();
        Gizmos.color = Color.cyan;
        for (int i = 0; i < forma.Length; i++)
            Gizmos.DrawLine(
                new Vector3(forma[i].x, forma[i].y, transform.position.z),
                new Vector3(forma[(i + 1) % forma.Length].x,
                            forma[(i + 1) % forma.Length].y, transform.position.z));
    }
}

