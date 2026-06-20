using System.Collections;
using UnityEngine;

public class PlayerPrototype : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.0f;
    public float rotationSpeed = 12f;

    [Header("Noise")]
    public float idleNoiseRadius = 0.5f;
    public float walkNoiseRadius = 2.0f;
    public float runNoiseRadius = 5.0f;
    public float attackNoiseRadius = 7.0f;

    [Header("Combat")]
    public Transform attackPoint;
    public float attackRadius = 1.4f;
    public int attackDamage = 1;
    public float attackNoiseDuration = 0.35f;
    public float attackCooldown = 0.6f;

    public float CurrentNoiseRadius { get; private set; }

    private CharacterController controller;
    private float attackNoiseTimer;
    private float cooldownTimer;

    [Header("Hit Feedback")]
    public Color hitColor = Color.magenta;
    public float hitFlashDuration = 0.15f;

    private Renderer playerRenderer;
    private Color originalColor;
    private Coroutine hitFlashCoroutine;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerRenderer = GetComponent<Renderer>();

        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }

        CurrentNoiseRadius = idleNoiseRadius;
    }

    private void Update()
    {
        HandleMovement();
        HandleAttack();
        UpdateNoise();
    }

    private void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 input = new Vector3(x, 0f, z).normalized;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        float speed = isRunning ? runSpeed : walkSpeed;

        if (input.sqrMagnitude > 0.01f)
        {
            controller.SimpleMove(input * speed);

            Quaternion targetRotation = Quaternion.LookRotation(input);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }
        else
        {
            controller.SimpleMove(Vector3.zero);
        }
    }

    private void HandleAttack()
    {
        cooldownTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0) && cooldownTimer <= 0f)
        {
            cooldownTimer = attackCooldown;
            attackNoiseTimer = attackNoiseDuration;

            Vector3 center = attackPoint != null
                ? attackPoint.position
                : transform.position + transform.forward * 1.2f;

            Collider[] hits = Physics.OverlapSphere(center, attackRadius);

            foreach (Collider hit in hits)
            {
                EnemyPrototype enemy = hit.GetComponent<EnemyPrototype>();

                if (enemy != null)
                {
                    enemy.TakeDamage(attackDamage);
                }
            }

            Debug.Log("Player attacked.");
        }
    }

    private void UpdateNoise()
    {
        if (attackNoiseTimer > 0f)
        {
            attackNoiseTimer -= Time.deltaTime;
            CurrentNoiseRadius = attackNoiseRadius;
            return;
        }

        bool isMoving =
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (!isMoving)
        {
            CurrentNoiseRadius = idleNoiseRadius;
        }
        else if (isRunning)
        {
            CurrentNoiseRadius = runNoiseRadius;
        }
        else
        {
            CurrentNoiseRadius = walkNoiseRadius;
        }
    }

    public void ReceiveHit(GameObject attacker)
    {
        string attackerName = attacker != null
            ? attacker.name
            : "Unknown attacker";

        Debug.Log($"{gameObject.name} was hit by {attackerName}.");

        if (playerRenderer == null)
        {
            return;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(FlashHitColor());
    }

    private IEnumerator FlashHitColor()
    {
        playerRenderer.material.color = hitColor;

        yield return new WaitForSeconds(hitFlashDuration);

        playerRenderer.material.color = originalColor;
        hitFlashCoroutine = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentNoiseRadius);

        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.2f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, attackRadius);
    }
}