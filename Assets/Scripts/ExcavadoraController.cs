using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ExcavadoraController : MonoBehaviour
{
    [Header("Configuración de Físicas (Inercia)")]
    public float aceleracion = 25f;
    public float velocidadMaxima = 6f;
    public float friccionFrenado = 0.95f;
    public float fuerzaSalto = 12f;

    [Header("Componentes")]
    private Rigidbody2D rb;

    [Tooltip("Asigna el Animator del chasis, no el del taladro o la torreta.")]
    [SerializeField] private Animator anim;

    [Header("Armas")]
    public Animator animTaladro;
    public Transform torreta;
    public float velocidadRotacionTorreta = 300f;
    public Animator animTorreta;
    public Animator animCanon;
    public TaladroDestructivo taladroDestructivo;

    public TaladroTermico taladroTermico;

    [Header("Munición del cañón")]
    [Min(1)] public int capacidadMunicionCanon = 10;
    [Min(0)] public int municionInicialCanon = 6;
    [SerializeField] private int municionActualCanon;
    private int ultimoFrameDisparoCanon = -1;
    [Tooltip("Tiempo maximo hasta que el evento de disparo lanza el proyectil.")]
    [Min(0.1f)] public float tiempoMaximoEsperaCanon = 2f;
    private bool disparoCanonPendiente;
    private float limiteEsperaCanon;

    public int MunicionActualCanon => municionActualCanon;
    public int CapacidadMunicionCanon => Mathf.Max(1, capacidadMunicionCanon);

    [Header("Disparo Torreta")]
    public GameObject proyectilPrefab;
    public Transform puntoDisparo1;
    public Transform puntoDisparo2;
    public float velocidadBala = 15f;
    public float retrasoEntreTiros = 0.1f;
    public float tiempoRecarga = 0.5f;
    private bool puedeDisparar = true;

    [Header("Suelo")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    [Header("Comprobación adicional de suelo")]
    public Collider2D colliderSuelo;

    [Min(0f)]
    public float margenComprobacionSuelo = 0.05f;

    private readonly RaycastHit2D[] resultadosSuelo =
        new RaycastHit2D[16];

    private bool isGrounded;
    private readonly ContactPoint2D[] contactosSuelo = new ContactPoint2D[32];
    private bool preparandoSalto;
    private bool impulsoSaltoPendiente;

    [Header("Recuperación del salto")]
    [Tooltip("Debe ser mayor que el tiempo hasta el evento de despegue.")]
    [Min(0.1f)] public float tiempoMaximoPreparacionSalto = 1.2f;
    private float tiempoPreparandoSalto;

    private static readonly int estadoBase =
        Animator.StringToHash("Base Layer.ExcavadoraBase");
    private static readonly int estadoAire =
        Animator.StringToHash("Base Layer.ExcavadoraAire");
    private static readonly int estadoDespegue =
        Animator.StringToHash("Base Layer.ExcavadoraDespegue");

    private ControlesExcavadora controles;
    private Vector2 inputMovimiento;
    public bool EnSecuencia { get; private set; }
    private float movimientoSecuencia;
    private bool taladroSecuencia;
    private bool termicoHabilitadoAntes;

    [Header("Animación de aire")]
    [Min(0f)]
    public float toleranciaAireAnimacion = 0.1f;

    private float tiempoSinSueloVisual;

    private void Awake()
    {
        controles = new ControlesExcavadora();
        rb = GetComponent<Rigidbody2D>();
        municionActualCanon = Mathf.Clamp(
            municionInicialCanon, 0, CapacidadMunicionCanon);

        if (anim == null)
            anim = GetComponentInChildren<Animator>();

        controles.Excavadora.Saltar.performed += ctx => Saltar();
        controles.Excavadora.DispararTorreta.performed += ctx => FuegoTorreta();
        controles.Excavadora.DispararCanon.performed +=ctx => FuegoCanon();
    }

    private void OnEnable()
    {
        controles.Excavadora.Enable();
    }

    private void OnDisable()
    {
        if (EnSecuencia)
            EstablecerControlSecuencia(false);
        controles.Excavadora.Disable();
        CancelarSaltoPendiente();
        CancelarDisparoCanon();
        if (taladroTermico != null)
        {
            taladroTermico.EstablecerActivo(false);
        }
        if (taladroDestructivo != null)
            taladroDestructivo.EstablecerActivo(false);
        AudioJuegoSimple.DetenerTaladroSiExiste();
    }

    private void Update()
    {
        if (disparoCanonPendiente &&
            (Time.time >= limiteEsperaCanon ||
                animCanon == null || !animCanon.isActiveAndEnabled))
            CancelarDisparoCanon();

        inputMovimiento = EnSecuencia ? new Vector2(movimientoSecuencia, 0f) :
            controles.Excavadora.Mover.ReadValue<Vector2>();

        bool estaPresionando =
            EnSecuencia ? taladroSecuencia : controles.Excavadora.ActivarTaladro.IsPressed();

        bool taladroFunciona = estaPresionando;

        if (taladroTermico != null && !EnSecuencia)
        {
            taladroTermico.EstablecerActivo(estaPresionando);
            taladroFunciona = taladroTermico.PuedeTaladrar;
        }

        if (taladroDestructivo != null)
        {
            taladroDestructivo.EstablecerActivo(taladroFunciona && !EnSecuencia);
        }

        if (animTaladro != null && animTaladro.isActiveAndEnabled)
        {
            animTaladro.SetBool("Taladrando", taladroFunciona);
        }
        AudioJuegoSimple.Instancia.SostenerTaladro(taladroFunciona);

        bool seEstaMoviendo =
        Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        // Ignora pérdidas muy breves de suelo para la animación.
        if (isGrounded)
        {
            tiempoSinSueloVisual = 0f;
        }
        else
        {
            tiempoSinSueloVisual += Time.deltaTime;
        }

        bool enSueloVisual =
            isGrounded ||
            tiempoSinSueloVisual < toleranciaAireAnimacion;

        if (anim != null)
        {
            anim.SetBool("isMoving", seEstaMoviendo);
            anim.SetBool("EnSuelo", enSueloVisual);

            anim.SetFloat(
                "VelocidadY",
                enSueloVisual ? 0f : rb.linearVelocity.y
            );

            anim.SetFloat(
                "VelocidadX",
                rb.linearVelocity.x
            );

            // Si ya volvió al suelo, Despegue no debe bloquear otro salto.
            // No interrumpe una preparación nueva ni el aterrizaje normal.
            if (isGrounded && !preparandoSalto &&
                !anim.IsInTransition(0) &&
                anim.GetCurrentAnimatorStateInfo(0).fullPathHash == estadoDespegue)
            {
                RecuperarAnimacionSalto();
            }
        }

        if (inputMovimiento.magnitude > 0)
        {
            float anguloObjetivo = Mathf.Atan2(
                inputMovimiento.y,
                inputMovimiento.x
            ) * Mathf.Rad2Deg;

            if (transform.localScale.x < 0)
            {
                anguloObjetivo = 180f - anguloObjetivo;
            }

            if (torreta != null)
            {
                float anguloActual = torreta.localEulerAngles.z;

                float nuevoAngulo = Mathf.MoveTowardsAngle(
                    anguloActual,
                    anguloObjetivo,
                    velocidadRotacionTorreta * Time.deltaTime
                );

                torreta.localRotation =
                    Quaternion.Euler(0, 0, nuevoAngulo);
            }
        }
    }

    private void FixedUpdate()
    {
        isGrounded = DetectarSuelo();

        if (preparandoSalto)
        {
            tiempoPreparandoSalto += Time.fixedDeltaTime;

            // Un golpe puede interrumpir el clip antes de que llegue su evento.
            // Cancela el intento, libera el salto y recupera la animación.
            if (!isGrounded ||
                (!impulsoSaltoPendiente && tiempoPreparandoSalto >=
                    Mathf.Max(0.1f, tiempoMaximoPreparacionSalto)))
            {
                CancelarSaltoPendiente();
            }
        }

        // Aplica el impulso solicitado por el evento de animación.
        if (preparandoSalto && impulsoSaltoPendiente && isGrounded)
        {
            preparandoSalto = false;
            impulsoSaltoPendiente = false;
            tiempoPreparandoSalto = 0f;
            isGrounded = false;

            tiempoSinSueloVisual =
            toleranciaAireAnimacion + Time.fixedDeltaTime;

            rb.linearVelocity =
                new Vector2(rb.linearVelocity.x, 0f);

            rb.AddForce(
                Vector2.up * fuerzaSalto,
                ForceMode2D.Impulse
            );

            if (anim != null)
            {
                anim.ResetTrigger("Salto");
                anim.SetBool("EnSuelo", false);
            }
        }

        if (Mathf.Abs(inputMovimiento.x) > 0)
        {
            rb.AddForce(
                new Vector2(inputMovimiento.x * aceleracion, 0),
                ForceMode2D.Force
            );
        }
        else
        {
            rb.linearVelocity = new Vector2(
                rb.linearVelocity.x * friccionFrenado,
                rb.linearVelocity.y
            );
        }

        if (Mathf.Abs(rb.linearVelocity.x) > velocidadMaxima)
        {
            float direccion = Mathf.Sign(rb.linearVelocity.x);

            rb.linearVelocity = new Vector2(
                direccion * velocidadMaxima,
                rb.linearVelocity.y
            );
        }
    }

    private bool DetectarSuelo()
    {
        // Conserva el comportamiento al despegar.
        if (rb.linearVelocity.y > 0.1f)
            return false;

        ContactFilter2D filtro = new ContactFilter2D();

        filtro.SetLayerMask(groundLayer);
        filtro.useTriggers = false;

        // Primero revisa los contactos físicos actuales.
        int cantidad = rb.GetContacts(
            filtro,
            contactosSuelo
        );

        for (int i = 0; i < cantidad; i++)
        {
            ContactPoint2D contacto = contactosSuelo[i];

            if (contacto.enabled &&
                contacto.normal.y >= 0.65f)
            {
                return true;
            }
        }

        // Si pierde el contacto brevemente,
        // comprueba si el piso sigue justo debajo.
        if (colliderSuelo == null ||
            !colliderSuelo.enabled ||
            colliderSuelo.isTrigger ||
            !colliderSuelo.gameObject.activeInHierarchy)
        {
            return false;
        }

        int encontrados = colliderSuelo.Cast(
            Vector2.down,
            filtro,
            resultadosSuelo,
            margenComprobacionSuelo,
            true
        );

        for (int i = 0; i < encontrados; i++)
        {
            RaycastHit2D resultado = resultadosSuelo[i];

            // Ignora solapamientos iniciales:
            // su normal puede no representar la superficie.
            if (resultado.collider != null &&
                resultado.distance > 0f &&
                resultado.normal.y >= 0.65f)
            {
                return true;
            }
        }

        return false;
    }

    private void Saltar()
    {

        if (EnSecuencia || !isGrounded || preparandoSalto ||
            anim == null || !anim.isActiveAndEnabled)
            return;

        preparandoSalto = true;
        impulsoSaltoPendiente = false;
        tiempoPreparandoSalto = 0f;

        anim.ResetTrigger("Salto");
        anim.SetTrigger("Salto");
    }

    public void SolicitarDespegueDesdeAnimacion()
    {
        if (!isActiveAndEnabled || !preparandoSalto)
            return;

        // FixedUpdate aplicará el impulso.
        impulsoSaltoPendiente = true;
    }

    public void CancelarSaltoPendiente()
    {
        bool habiaPreparacion = preparandoSalto;
        preparandoSalto = false;
        impulsoSaltoPendiente = false;
        tiempoPreparandoSalto = 0f;

        if (anim != null)
        {
            anim.ResetTrigger("Salto");
        }

        if (habiaPreparacion)
            RecuperarAnimacionSalto();
    }

    private void RecuperarAnimacionSalto()
    {
        if (!isActiveAndEnabled || anim == null ||
            !anim.isActiveAndEnabled)
            return;

        anim.ResetTrigger("Salto");
        anim.SetBool("EnSuelo", isGrounded);
        anim.SetFloat("VelocidadY", isGrounded ? 0f : rb.linearVelocity.y);

        tiempoSinSueloVisual = isGrounded
            ? 0f
            : toleranciaAireAnimacion + Time.fixedDeltaTime;

        int destino = isGrounded ? estadoBase : estadoAire;
        if (anim.HasState(0, destino))
            anim.Play(destino, 0, 0f);
    }

    private void FuegoTorreta()
    {
        if (EnSecuencia)
            return;
        if (animTorreta != null && puedeDisparar)
        {
            animTorreta.SetTrigger("Disparar");
            StartCoroutine(DispararSecuencia());
        }
    }

    private IEnumerator DispararSecuencia()
    {
        puedeDisparar = false;

        CrearBala(puntoDisparo1);

        yield return new WaitForSeconds(retrasoEntreTiros);

        CrearBala(puntoDisparo2);

        yield return new WaitForSeconds(tiempoRecarga);

        puedeDisparar = true;
    }

    private void CrearBala(Transform punto)
    {
        if (punto != null && proyectilPrefab != null)
        {
            GameObject bala = Instantiate(
                proyectilPrefab,
                punto.position,
                punto.rotation
            );
            AudioJuegoSimple.Instancia.Disparo();

            bala.GetComponent<Rigidbody2D>().linearVelocity =
                punto.right * velocidadBala;

            Destroy(bala, 3f);
        }
    }

    private void FuegoCanon()
    {
        if (EnSecuencia || disparoCanonPendiente || municionActualCanon <= 0 ||
            ultimoFrameDisparoCanon == Time.frameCount ||
            animCanon == null || !animCanon.isActiveAndEnabled)
            return;

        // Espera a que termine el disparo anterior.
        if (animCanon.IsInTransition(0))
            return;

        if (!animCanon.GetCurrentAnimatorStateInfo(0)
            .IsName("CanonIdle"))
            return;

        // Reserva el intento. El proyectil confirma el consumo en Lanzar.
        disparoCanonPendiente = true;
        limiteEsperaCanon = Time.time + Mathf.Max(0.1f, tiempoMaximoEsperaCanon);
        ultimoFrameDisparoCanon = Time.frameCount;
        animCanon.ResetTrigger("Disparar");
        animCanon.SetTrigger("Disparar");
    }

    public bool ConfirmarDisparoCanon()
    {
        if (!isActiveAndEnabled || !disparoCanonPendiente ||
            municionActualCanon <= 0 || Time.time >= limiteEsperaCanon)
            return false;

        disparoCanonPendiente = false;
        municionActualCanon--;
        AudioJuegoSimple.Instancia.Canon();
        return true;
    }

    private void CancelarDisparoCanon()
    {
        disparoCanonPendiente = false;
        if (animCanon != null)
            animCanon.ResetTrigger("Disparar");
    }

    public int RecargarCanon(int cantidad)
    {
        if (cantidad <= 0)
            return 0;

        int espacioLibre = Mathf.Max(
            0, CapacidadMunicionCanon - municionActualCanon);
        int recibido = Mathf.Min(cantidad, espacioLibre);
        municionActualCanon += recibido;
        return recibido;
    }

    // El controller permanece activo para conservar las animaciones y las fisicas.
    public void EstablecerControlSecuencia(bool activar, float movimientoX = 0f, bool girarTaladro = false)
    {
        if (activar && !EnSecuencia)
        {
            controles.Excavadora.Disable();
            StopAllCoroutines();
            puedeDisparar = true;
            CancelarSaltoPendiente();
            CancelarDisparoCanon();
            termicoHabilitadoAntes = taladroTermico != null && taladroTermico.enabled;
            if (taladroDestructivo != null)
                taladroDestructivo.EstablecerActivo(false);
            AudioJuegoSimple.DetenerTaladroSiExiste();
            if (taladroTermico != null)
            {
                taladroTermico.EstablecerActivo(false);
                taladroTermico.enabled = false;
            }
        }
        bool estabaEnSecuencia = EnSecuencia;
        EnSecuencia = activar;
        movimientoSecuencia = activar ? Mathf.Clamp(movimientoX, -1f, 1f) : 0f;
        taladroSecuencia = activar && girarTaladro;
        inputMovimiento = Vector2.zero;
        if (!activar && estabaEnSecuencia)
        {
            if (taladroTermico != null)
                taladroTermico.enabled = termicoHabilitadoAntes;
            if (isActiveAndEnabled)
                controles.Excavadora.Enable();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(
                groundCheck.position,
                groundCheckRadius
            );
        }
    }
}
