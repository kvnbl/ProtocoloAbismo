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

    private bool isGrounded;
    private readonly ContactPoint2D[] contactosSuelo = new ContactPoint2D[32];
    private bool preparandoSalto;
    private bool impulsoSaltoPendiente;

    private ControlesExcavadora controles;
    private Vector2 inputMovimiento;

    private void Awake()
    {
        controles = new ControlesExcavadora();
        rb = GetComponent<Rigidbody2D>();

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
        controles.Excavadora.Disable();
        CancelarSaltoPendiente();
    }

    private void Update()
    {
        inputMovimiento =
            controles.Excavadora.Mover.ReadValue<Vector2>();

        bool estaPresionando =
            controles.Excavadora.ActivarTaladro.IsPressed();
        if (taladroDestructivo != null)
        {
            taladroDestructivo.EstablecerActivo(estaPresionando);
        }

        if (animTaladro != null)
        {
            animTaladro.SetBool("Taladrando", estaPresionando);
        }

        bool seEstaMoviendo = Mathf.Abs(rb.linearVelocity.x) > 0.1f;

        if (anim != null)
        {
            anim.SetBool("isMoving", seEstaMoviendo);
            anim.SetBool("EnSuelo", isGrounded);
            anim.SetFloat("VelocidadY", rb.linearVelocity.y);
            anim.SetFloat("VelocidadX", rb.linearVelocity.x);
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

        // Cancela la preparación si pierde el suelo antes de despegar.
        if (preparandoSalto && !isGrounded)
        {
            CancelarSaltoPendiente();
        }

        // Aplica el impulso solicitado por el evento de animación.
        if (preparandoSalto && impulsoSaltoPendiente && isGrounded)
        {
            preparandoSalto = false;
            impulsoSaltoPendiente = false;
            isGrounded = false;

            rb.linearVelocity =
                new Vector2(rb.linearVelocity.x, 0f);

            rb.AddForce(
                Vector2.up * fuerzaSalto,
                ForceMode2D.Impulse
            );

            if (anim != null)
            {
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
        // Mientras sube, no puede considerarse aterrizada.
        if (rb.linearVelocity.y > 0.1f)
            return false;

        ContactFilter2D filtro = new ContactFilter2D();
        filtro.SetLayerMask(groundLayer);
        filtro.useTriggers = false;

        int cantidad = rb.GetContacts(filtro, contactosSuelo);

        for (int i = 0; i < cantidad; i++)
        {
            ContactPoint2D contacto = contactosSuelo[i];

            // Ignora contactos que permiten atravesar la plataforma.
            if (!contacto.enabled)
                continue;

            // Solo cuenta una superficie que sostiene desde abajo.
            // Las paredes y los techos quedan descartados.
            if (contacto.normal.y >= 0.65f)
                return true;
        }

        return false;
    }

    private void Saltar()
    {

        if (!isGrounded || preparandoSalto || anim == null)
            return;

        preparandoSalto = true;
        impulsoSaltoPendiente = false;

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
        preparandoSalto = false;
        impulsoSaltoPendiente = false;

        if (anim != null)
        {
            anim.ResetTrigger("Salto");
        }
    }

    private void FuegoTorreta()
    {
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

            bala.GetComponent<Rigidbody2D>().linearVelocity =
                punto.right * velocidadBala;

            Destroy(bala, 3f);
        }
    }

    private void FuegoCanon()
    {
        if (animCanon == null || !animCanon.isActiveAndEnabled)
            return;

        // Espera a que termine el disparo anterior.
        if (animCanon.IsInTransition(0))
            return;

        if (!animCanon.GetCurrentAnimatorStateInfo(0)
            .IsName("CanonIdle"))
            return;

        animCanon.SetTrigger("Disparar");
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