using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerPrototype : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 6.0f;
    public float rotationSpeed = 12.0f;

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

    [Header("Hit Feedback")]
    public Color hitColor = Color.magenta;
    public float hitFlashDuration = 0.15f;

    public float CurrentNoiseRadius { get; private set; }
    public bool IsMakingActiveNoise { get; private set; }

    private CharacterController controller;
    private Renderer playerRenderer;
    private Color originalColor;
    private Coroutine hitFlashCoroutine;
    private float attackNoiseTimer;
    private float cooldownTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerRenderer = GetComponent<Renderer>();

        if (playerRenderer != null)
        {
            originalColor = playerRenderer.material.color;
        }

        CurrentNoiseRadius = idleNoiseRadius;
        IsMakingActiveNoise = false;
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

        Vector3 input = new Vector3(x, 0.0f, z).normalized;
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float speed = isRunning ? runSpeed : walkSpeed;

        if (input.sqrMagnitude > 0.01f)
        {
            controller.SimpleMove(input * speed);

            Quaternion targetRotation =
                Quaternion.LookRotation(input);

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

        if (
            !Input.GetMouseButtonDown(0) ||
            cooldownTimer > 0.0f
        )
        {
            return;
        }

        cooldownTimer = attackCooldown;
        attackNoiseTimer = attackNoiseDuration;

        Collider[] hits = Physics.OverlapSphere(
            GetAttackCenter(),
            attackRadius,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        HashSet<EnemyPrototype> damagedEnemies =
            new HashSet<EnemyPrototype>();

        foreach (Collider hit in hits)
        {
            EnemyPrototype enemy =
                hit.GetComponentInParent<EnemyPrototype>();

            if (enemy == null || damagedEnemies.Contains(enemy))
            {
                continue;
            }

            damagedEnemies.Add(enemy);
            enemy.TakeDamage(attackDamage, transform);
        }

        Debug.Log(
            $"Player attacked and hit {damagedEnemies.Count} enemy/enemies."
        );
    }

    private void UpdateNoise()
    {
        if (attackNoiseTimer > 0.0f)
        {
            attackNoiseTimer -= Time.deltaTime;
            CurrentNoiseRadius = attackNoiseRadius;
            IsMakingActiveNoise = true;
            return;
        }

        bool isMoving =
            Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f ||
            Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.01f;

        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (!isMoving)
        {
            CurrentNoiseRadius = idleNoiseRadius;
            IsMakingActiveNoise = false;
        }
        else if (isRunning)
        {
            CurrentNoiseRadius = runNoiseRadius;
            IsMakingActiveNoise = true;
        }
        else
        {
            CurrentNoiseRadius = walkNoiseRadius;
            IsMakingActiveNoise = true;
        }
    }

    public void ReceiveHit(GameObject attacker)
    {
        string attackerName = attacker != null
            ? attacker.name
            : "Unknown attacker";

        Debug.Log(
            $"{gameObject.name} was hit by {attackerName}."
        );

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

    private Vector3 GetAttackCenter()
    {
        return attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.2f;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            CurrentNoiseRadius
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            GetAttackCenter(),
            attackRadius
        );
    }
}
