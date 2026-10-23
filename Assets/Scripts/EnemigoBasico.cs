using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemigoBasico : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMaxima = 3;

    [Header("Jugador")]
    public Transform jugador;

    [Header("Movimiento")]
    public float velocidad = 1.5f;
    public float distanciaDeteccion = 8f;

    [Header("Separacion frente a la excavadora")]
    [Min(0f)] public float separacionAtaque = 0.25f;
    [Min(0f)] public float separacionMinima = 0.08f;
    [Min(0f)] public float margenVertical = 0.25f;
    [Tooltip("Opcional. Vacio: usa todos los colliders solidos del Rigidbody2D enemigo.")]
    public Collider2D cuerpoEnemigo;
    [Tooltip("Opcional. Vacio: incluye los colliders solidos de la excavadora, tambien el taladro.")]
    public Collider2D cuerpoJugador;

    [Header("Paso atras al preparar")]
    [Min(0f)] public float velocidadPasoAtras = 1.2f;
    [Min(0f)] public float duracionPasoAtras = 0.2f;

    [Header("Embestida")]
    public float velocidadEmbestida = 6f;
    public float fuerzaSalto = 4.5f;
    public float duracionMaximaEmbestida = 1f;
    public int danio = 1;

    [Header("Retirada despues de atacar")]
    [Min(0f)] public float velocidadRetroceso = 1.4f;
    [Min(0f)] public float distanciaRetirada = 0.8f;
    [Min(0f)] public float duracionMaximaRetirada = 1.2f;
    [Min(0f)] public float pausaAterrizaje = 0.2f;
    [Min(0f)] public float esperaEntreAtaques = 1.2f;
    [Min(0f)] public float variacionEspera = 0.6f;

    [Header("Combate en grupo")]
    [Tooltip("Pequena variacion individual para no detenerse todos en el mismo punto.")]
    [Min(0f)] public float variacionSeparacion = 0.12f;
    [Min(0.1f)] public float tiempoMaximoPreparacion = 2f;

    [Header("Suelo")]
    public LayerMask capasSuelo;

    [Header("Aspecto")]
    public Animator anim;
    public SpriteRenderer imagen;
    public EfectosEnemigo efectos;

    private enum Estado
    {
        Perseguir, Preparar, Embestir, Retroceder,
        Aterrizar, Retirarse, Recuperar
    }

    private Rigidbody2D rb;
    private Rigidbody2D rbJugador;
    private Transform jugadorAnterior;
    private Collider2D[] collidersEnemigo;
    private Collider2D[] collidersJugador;
    private readonly ContactPoint2D[] contactos = new ContactPoint2D[32];
    private Estado estado;
    private int vidaActual;
    private bool muerto;
    private float tiempoEstado;
    private float tiempoPasoAtras;
    private float direccionAtaque = 1f;
    private float direccionRetroceso;
    private float posicionInicioRetirada;
    private float esperaActual;
    private float ajusteSeparacion;
    private float primerAtaquePermitido;

    public bool EstaVivo => !muerto && isActiveAndEnabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        vidaActual = vidaMaxima;
        collidersEnemigo = GetComponentsInChildren<Collider2D>(true);
        ajusteSeparacion = Random.Range(
            -Mathf.Max(0f, variacionSeparacion),
            Mathf.Max(0f, variacionSeparacion));

        if (imagen == null)
            imagen = GetComponentInChildren<SpriteRenderer>();
        if (anim == null)
            anim = GetComponentInChildren<Animator>();
        if (efectos == null)
            efectos = GetComponent<EfectosEnemigo>();
    }

    private void Start()
    {
        if (jugador == null)
        {
            VidaJugador encontrado = FindFirstObjectByType<VidaJugador>();
            if (encontrado != null)
                jugador = encontrado.transform;
        }

        ActualizarReferenciasJugador();
        primerAtaquePermitido = Time.time +
            Random.Range(0f, Mathf.Max(0f, variacionEspera));
        CambiarEstado(Estado.Perseguir);
    }

    private void FixedUpdate()
    {
        if (muerto)
            return;

        ActualizarReferenciasJugador();
        tiempoEstado += Time.fixedDeltaTime;
        bool enSuelo = EstaEnSuelo();

        switch (estado)
        {
            case Estado.Perseguir:
                PerseguirJugador(enSuelo);
                break;

            case Estado.Preparar:
                ActualizarPreparacion(enSuelo);
                if (estado == Estado.Preparar &&
                    tiempoEstado >= tiempoMaximoPreparacion)
                    CambiarEstado(Estado.Recuperar);
                break;

            case Estado.Embestir:
                if (enSuelo && tiempoEstado > 0.12f)
                {
                    FinalizarAtaque(-direccionAtaque);
                }
                else
                {
                    float avance = tiempoEstado <= duracionMaximaEmbestida
                        ? direccionAtaque * velocidadEmbestida : 0f;
                    MoverHorizontal(avance);
                }
                break;

            case Estado.Retroceder:
                // Termina de caer antes de retirarse caminando.
                MoverHorizontal(0f);
                if (enSuelo)
                    CambiarEstado(Estado.Aterrizar);
                break;

            case Estado.Aterrizar:
                MoverHorizontal(0f);
                if (enSuelo && tiempoEstado >= pausaAterrizaje)
                    CambiarEstado(Estado.Retirarse);
                break;

            case Estado.Retirarse:
                ActualizarRetirada(enSuelo);
                break;

            case Estado.Recuperar:
                MoverHorizontal(0f);
                if (enSuelo && tiempoEstado >= esperaActual)
                    CambiarEstado(Estado.Perseguir);
                break;
        }
    }

    private void PerseguirJugador(bool enSuelo)
    {
        float movimientoX = 0f;

        if (jugador != null)
        {
            Vector2 diferencia = (Vector2)jugador.position - rb.position;
            if (diferencia.magnitude <= distanciaDeteccion)
            {
                float separacion;
                bool aSuAltura;
                float direccion;
                bool usaColliders = MedirJugador(
                    out separacion, out aSuAltura, out direccion);

                if (!usaColliders)
                {
                    MoverHorizontal(0f);
                    if (anim != null)
                        anim.SetBool("Caminando", false);
                    return;
                }

                direccionAtaque = direccion;
                if (imagen != null)
                    imagen.flipX = direccionAtaque < 0f;

                float distanciaObjetivo = Mathf.Max(
                    0f, separacionAtaque + ajusteSeparacion, separacionMinima);
                bool aDistancia = separacion <= distanciaObjetivo + 0.01f;

                if (enSuelo && aDistancia && aSuAltura)
                {
                    MoverHorizontal(0f);
                    // Cada enemigo decide por si mismo; no hay turnos de ataque.
                    if (Time.time >= primerAtaquePermitido)
                    {
                        CambiarEstado(Estado.Preparar);
                        ActualizarPreparacion(enSuelo);
                    }
                    else if (anim != null)
                        anim.SetBool("Caminando", false);
                    return;
                }

                if (!aDistancia)
                {
                    // Reduce el ultimo paso para no avanzar dentro del jugador.
                    float avancePermitido = Mathf.Max(
                        0f, separacion - distanciaObjetivo);
                    float rapidez = Mathf.Min(
                        Mathf.Max(0f, velocidad),
                        avancePermitido / Time.fixedDeltaTime);
                    movimientoX = direccionAtaque * rapidez;
                }
            }
        }

        MoverHorizontal(movimientoX);
        if (anim != null)
            anim.SetBool("Caminando", Mathf.Abs(rb.linearVelocity.x) > 0.01f);
    }

    private void ActualizarRetirada(bool enSuelo)
    {
        MoverHorizontal(0f);
        if (tiempoEstado >= duracionMaximaRetirada ||
            Mathf.Abs(rb.position.x - posicionInicioRetirada) >= distanciaRetirada)
        {
            CambiarEstado(Estado.Recuperar);
            return;
        }

        // Un contacto de suelo perdido brevemente no cancela la retirada.
        if (!enSuelo)
        {
            if (anim != null)
                anim.SetBool("Caminando", false);
            return;
        }

        // Mira al jugador mientras se desplaza en sentido contrario.
        if (jugador != null)
        {
            float diferenciaX = jugador.position.x - rb.position.x;
            if (Mathf.Abs(diferenciaX) > 0.05f)
                direccionAtaque = Mathf.Sign(diferenciaX);
            if (imagen != null)
                imagen.flipX = direccionAtaque < 0f;
            direccionRetroceso = -direccionAtaque;
        }

        float restante = Mathf.Max(0f, distanciaRetirada -
            Mathf.Abs(rb.position.x - posicionInicioRetirada));
        float rapidez = Mathf.Min(Mathf.Max(0f, velocidadRetroceso),
            restante / Time.fixedDeltaTime);
        MoverHorizontal(direccionRetroceso * rapidez);
        if (anim != null)
            anim.SetBool("Caminando", Mathf.Abs(rb.linearVelocity.x) > 0.01f);
    }

    private void ActualizarPreparacion(bool enSuelo)
    {
        MoverHorizontal(0f);
        if (!enSuelo || jugador == null)
        {
            CambiarEstado(Estado.Perseguir);
            return;
        }

        float separacion;
        bool aSuAltura;
        float direccion;
        bool usaColliders = MedirJugador(
            out separacion, out aSuAltura, out direccion);

        if (!usaColliders || !aSuAltura)
            return;

        // Deja hasta 0.2 unidades extra al preparar el ataque.
        float separacionDeseada = Mathf.Max(
            0f, separacionAtaque + ajusteSeparacion, separacionMinima) + 0.2f;
        if (separacion >= separacionDeseada ||
            tiempoPasoAtras >= duracionPasoAtras)
            return;

        // El tiempo se consume solo al retroceder, sin reiniciar el estado.
        float paso = Mathf.Min(
            Time.fixedDeltaTime, duracionPasoAtras - tiempoPasoAtras);
        float desplazamiento = Mathf.Min(
            Mathf.Max(0f, velocidadPasoAtras) * paso,
            separacionDeseada - separacion);
        MoverHorizontal(-direccion * desplazamiento / Time.fixedDeltaTime);
        tiempoPasoAtras += paso;

        // El Animation Event conserva el control del salto.
    }

    private void ActualizarReferenciasJugador()
    {
        if (jugador == jugadorAnterior)
            return;

        jugadorAnterior = jugador;
        rbJugador = null;
        collidersJugador = null;
        if (jugador == null)
            return;

        rbJugador = jugador.GetComponentInParent<Rigidbody2D>();
        collidersJugador = rbJugador != null
            ? rbJugador.GetComponentsInChildren<Collider2D>(true)
            : jugador.GetComponentsInChildren<Collider2D>(true);
    }

    private static bool ColliderValido(Collider2D cuerpo)
    {
        return cuerpo != null && cuerpo.enabled &&
            cuerpo.gameObject.activeInHierarchy && !cuerpo.isTrigger;
    }

    private static bool ObtenerLimites(
        Collider2D elegido, Collider2D[] candidatos,
        Rigidbody2D propietario, out Bounds limites)
    {
        limites = new Bounds();
        if (ColliderValido(elegido))
        {
            limites = elegido.bounds;
            return true;
        }

        bool encontrado = false;
        if (candidatos == null)
            return false;

        foreach (Collider2D candidato in candidatos)
        {
            if (!ColliderValido(candidato) ||
                candidato.attachedRigidbody != propietario)
                continue;

            if (!encontrado)
                limites = candidato.bounds;
            else
                limites.Encapsulate(candidato.bounds);
            encontrado = true;
        }
        return encontrado;
    }

    private bool MedirJugador(
        out float separacion, out bool aSuAltura, out float direccion)
    {
        Vector2 diferencia = (Vector2)jugador.position - rb.position;
        direccion = Mathf.Abs(diferencia.x) > 0.01f
            ? Mathf.Sign(diferencia.x) : direccionAtaque;

        Bounds enemigo;
        Bounds objetivo;
        if (ObtenerLimites(cuerpoEnemigo, collidersEnemigo, rb, out enemigo) &&
            ObtenerLimites(cuerpoJugador, collidersJugador, rbJugador, out objetivo))
        {
            float diferenciaCentros = objetivo.center.x - enemigo.center.x;
            if (Mathf.Abs(diferenciaCentros) > 0.01f)
                direccion = Mathf.Sign(diferenciaCentros);

            // Negativo significa que los limites horizontales se superponen.
            separacion = direccion > 0f
                ? objetivo.min.x - enemigo.max.x
                : enemigo.min.x - objetivo.max.x;
            aSuAltura = enemigo.max.y >= objetivo.min.y - margenVertical &&
                objetivo.max.y >= enemigo.min.y - margenVertical;
            return true;
        }

        separacion = Mathf.Abs(diferencia.x);
        aSuAltura = Mathf.Abs(diferencia.y) <= 1.5f;
        return false;
    }

    private bool EstaEnSuelo()
    {
        if (rb.linearVelocity.y > 0.1f)
            return false;

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasSuelo);
        filtro.useTriggers = false;
        int cantidad = rb.GetContacts(filtro, contactos);
        for (int i = 0; i < cantidad; i++)
        {
            if (contactos[i].enabled && contactos[i].normal.y >= 0.65f)
                return true;
        }
        return false;
    }

    private void MoverHorizontal(float velocidadX)
    {
        rb.linearVelocity = new Vector2(velocidadX, rb.linearVelocity.y);
    }

    private void CambiarEstado(Estado nuevo)
    {
        estado = nuevo;
        tiempoEstado = 0f;
        tiempoPasoAtras = 0f;
        if (nuevo == Estado.Retirarse)
            posicionInicioRetirada = rb.position.x;
        if (nuevo == Estado.Recuperar)
            esperaActual = Mathf.Max(0f, esperaEntreAtaques) +
                Random.Range(0f, Mathf.Max(0f, variacionEspera));
        if (anim == null)
            return;

        anim.SetBool("Caminando", false);
        switch (nuevo)
        {
            case Estado.Perseguir:
            case Estado.Retirarse:
            case Estado.Recuperar:
                anim.Play("Base Layer.Enemigo1_Idle", 0, 0f);
                break;
            case Estado.Preparar:
                anim.Play("Base Layer.Enemigo1_Preparacion", 0, 0f);
                break;
            case Estado.Embestir:
                anim.Play("Base Layer.Enemigo1_Embestida", 0, 0f);
                break;
            case Estado.Aterrizar:
                anim.Play("Base Layer.Enemigo1_Aterrizar", 0, 0f);
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D colision)
    {
        IntentarGolpear(colision);
    }

    private void OnCollisionStay2D(Collision2D colision)
    {
        IntentarGolpear(colision);
    }

    private void IntentarGolpear(Collision2D colision)
    {
        if (muerto || !colision.enabled)
            return;

        AplastamientoExcavadora aplastamiento =
            colision.collider.GetComponentInParent<AplastamientoExcavadora>();
        if (aplastamiento != null &&
            aplastamiento.IntentarDesdeContacto(this, colision))
            return;

        if (muerto || estado != Estado.Embestir ||
            tiempoEstado > duracionMaximaEmbestida || !colision.enabled)
            return;

        VidaJugador vida = colision.collider.GetComponentInParent<VidaJugador>();
        if (vida == null)
            return;

        float diferenciaX = rb.position.x - vida.transform.position.x;
        float alejamiento = Mathf.Abs(diferenciaX) > 0.05f
            ? Mathf.Sign(diferenciaX) : -direccionAtaque;
        FinalizarAtaque(alejamiento);
        vida.RecibirDanio(danio);
    }

    private void FinalizarAtaque(float direccion)
    {
        direccionRetroceso = direccion;
        // Frena el ataque. El retroceso posterior sera caminando.
        rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y));
        CambiarEstado(EstaEnSuelo() ? Estado.Aterrizar : Estado.Retroceder);
    }

    public void SaltarDesdeAnimacion()
    {
        if (muerto || estado != Estado.Preparar)
            return;
        if (jugador == null || !EstaEnSuelo())
        {
            CambiarEstado(Estado.Perseguir);
            return;
        }

        CambiarEstado(Estado.Embestir);
        rb.linearVelocity = new Vector2(
            direccionAtaque * velocidadEmbestida,
            fuerzaSalto);
    }

    public void MorirPorAplastamiento()
    {
        // El basico muere sin depender del dano configurado en el jugador.
        if (!muerto)
            AplicarDanio(vidaActual, TipoMuerteEnemigo.Aplastamiento);
    }

    public void RecibirDanio(int cantidad)
    {
        AplicarDanio(cantidad, TipoMuerteEnemigo.Normal);
    }

    public void RecibirDanioCanon(int cantidad)
    {
        AplicarDanio(cantidad, TipoMuerteEnemigo.Quemado);
    }

    public void RecibirDanioBala(int cantidad, Vector2 punto, Vector2 direccionSalida)
    {
        if (muerto || cantidad <= 0)
            return;
        if (efectos != null && efectos.isActiveAndEnabled)
            efectos.MostrarSangre(punto, direccionSalida);
        AplicarDanio(cantidad, TipoMuerteEnemigo.Normal);
    }

    private void AplicarDanio(int cantidad, TipoMuerteEnemigo tipo)
    {
        if (muerto || cantidad <= 0)
            return;

        vidaActual = Mathf.Max(0, vidaActual - cantidad);
        if (efectos != null && efectos.isActiveAndEnabled)
            efectos.Destellar();
        Debug.Log(name + ": quedan " + vidaActual + " vidas.", this);
        if (vidaActual == 0)
        {
            muerto = true;
            float velocidadY = rb.linearVelocity.y;
            rb.linearVelocity = Vector2.zero;
            // Retira la colision inmediatamente, antes de la siguiente simulacion.
            foreach (Collider2D cuerpo in collidersEnemigo)
            {
                if (cuerpo != null && cuerpo.attachedRigidbody == rb)
                    cuerpo.enabled = false;
            }
            rb.simulated = false;
            if (efectos != null && efectos.isActiveAndEnabled)
                efectos.IniciarMuerte(tipo, capasSuelo, velocidadY);
            else
                Destroy(gameObject);
        }
    }
}
