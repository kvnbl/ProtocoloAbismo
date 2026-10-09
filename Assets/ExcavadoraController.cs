using UnityEngine;
// No necesitamos "using UnityEngine.InputSystem" porque la clase autogenerada lo maneja sola

public class VehicleController : MonoBehaviour
{
    [Header("Configuración")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;

    [Header("Componentes")]
    public Animator animator;
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    [Header("Suelo")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;
    private bool isGrounded;

    // La clase autogenerada por Unity (igual que "ControlesNave" del PPT)
    private ControlesExcavadora controles;
    private Vector2 inputMovimiento;

    // Diapositiva 20: Todo suscrito en Awake()
    private void Awake()
    {
        controles = new ControlesExcavadora();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Suscribir el evento de Salto usando la sintaxis de lambda del PPT
        controles.Excavadora.Saltar.performed += ctx => Saltar();

        // Suscribir el evento del Taladro
        controles.Excavadora.Disparar.performed += ctx => ActivarTaladro();
    }

    // Diapositiva 12: Habilitar los controles
    private void OnEnable()
    {
        controles.Excavadora.Enable();
    }

    private void OnDisable()
    {
        controles.Excavadora.Disable();
    }

    // Diapositiva 13: Leer el input en Update()
    void Update()
    {
        inputMovimiento = controles.Excavadora.Mover.ReadValue<Vector2>();

        // Conectar la animación
        if (inputMovimiento.x != 0)
        {
            animator.SetBool("isMoving", true);
        }
        else
        {
            animator.SetBool("isMoving", false);
        }
    }

    // Diapositiva 15: Mover la nave en FixedUpdate()
    void FixedUpdate()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Aplicar la velocidad (En Unity 6 es linearVelocity, en anteriores es velocity)
        rb.linearVelocity = new Vector2(inputMovimiento.x * moveSpeed, rb.linearVelocity.y);

        // Voltear el sprite
        if (inputMovimiento.x > 0) spriteRenderer.flipX = false;
        else if (inputMovimiento.x < 0) spriteRenderer.flipX = true;
    }

    private void Saltar()
    {
        Debug.Log("Se presionó Espacio. ¿Toca el suelo?: " + isGrounded);

        if (isGrounded)
        {
            rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    private void ActivarTaladro()
    {
        Debug.Log("¡Taladro activado!");
        // Aquí conectaremos el script de rotación del taladro después
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}