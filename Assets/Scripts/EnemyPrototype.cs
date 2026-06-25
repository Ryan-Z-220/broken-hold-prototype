using UnityEngine;
using UnityEngine.AI;

public class EnemyPrototype : MonoBehaviour
{
    public enum EnemyState
    {
        Idle,
        Suspicious,
        Identifying,
        Chase,
        Attack,
        Defeated
    }

    [Header("References")]
    public Transform player;
    public PlayerPrototype playerNoise;
    public OutpostStateManager outpostManager;
    private Quaternion initialRotation;
    private NavMeshAgent navMeshAgent;
    private bool navMeshWarningShown;

    [Header("Perception")]
    public float hearingRadius = 4.0f;
    public float visionDistance = 8.0f;
    public float visionAngle = 80.0f;
    public float suspiciousDuration = 2.0f;

    [Header("Identification")]
    private bool playerIdentityEvaluated;
    private bool playerConfirmedHostile;

    [Header("Movement")]
    public float chaseSpeed = 2.8f;
    public float turnSpeed = 8.0f;
    public float attackDistance = 1.5f;

    [Header("Combat")]
    public Transform attackPoint;
    public float attackRadius = 1.0f;
    public int health = 2;
    public float attackCooldown = 1.5f;

    [Header("Debug")]
    public EnemyState currentState = EnemyState.Idle;

    private Vector3 lastKnownPlayerPosition;
    private float suspiciousTimer;
    private float attackTimer;
    private Renderer enemyRenderer;

    private void Awake()
    {
        enemyRenderer = GetComponent<Renderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        initialRotation = transform.rotation;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");

            if (playerObject != null)
            {
                player = playerObject.transform;
                playerNoise = playerObject.GetComponent<PlayerPrototype>();
            }
        }

        if (outpostManager == null)
        {
            outpostManager = FindObjectOfType<OutpostStateManager>();
        }

        SetColor(Color.gray);
    }

    private void Update()
    {
        if (currentState == EnemyState.Defeated || player == null || playerNoise == null)
        {
            return;
        }

        bool heardPlayer = CanHearPlayer();
        bool sawPlayer = CanSeePlayer();

        if (sawPlayer)
        {
            lastKnownPlayerPosition = player.position;

            if (!playerIdentityEvaluated)
            {
                currentState = EnemyState.Identifying;
            }
            else if (
                playerConfirmedHostile &&
                currentState != EnemyState.Attack &&
                currentState != EnemyState.Defeated
            )
            {
                currentState = EnemyState.Chase;
            }
        }
        else if (
            heardPlayer &&
            (currentState == EnemyState.Idle || currentState == EnemyState.Suspicious)
        )
        {
            currentState = EnemyState.Suspicious;
            lastKnownPlayerPosition = player.position;
            suspiciousTimer = suspiciousDuration;
        }

        switch (currentState)
        {
            case EnemyState.Idle:
                SetColor(Color.gray);
                HandleIdle();
                break;

            case EnemyState.Suspicious:
                SetColor(Color.yellow);
                HandleSuspicious();
                break;

            case EnemyState.Identifying:
                SetColor(Color.blue);
                HandleIdentifying();
                break;

            case EnemyState.Chase:
                SetColor(Color.red);
                HandleChase();
                break;

            case EnemyState.Attack:
                SetColor(new Color(1f, 0.2f, 0.2f));
                HandleAttack();
                break;
        }
    }

    private bool CanHearPlayer()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        // If player's noise area intersect with enemy's hearing area, player identified as detected by enemy.
        return distance <= hearingRadius + playerNoise.CurrentNoiseRadius;
    }

    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > visionDistance)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer.normalized);

        if (angle > visionAngle * 0.5f)
        {
            return false;
        }

        Vector3 eyePosition = transform.position + Vector3.up * 1.2f;
        Vector3 targetPosition = player.position + Vector3.up * 1.0f;
        Vector3 direction = targetPosition - eyePosition;

        if (Physics.Raycast(eyePosition, direction.normalized, out RaycastHit hit, visionDistance))
        {
            return hit.transform == player;
        }

        return false;
    }

    private void StopNavigation()
    {
        if (
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh
        )
        {
            return;
        }

        navMeshAgent.isStopped = true;
    }

    private void HandleIdle()
    {
        StopNavigation();

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            initialRotation,
            turnSpeed * Time.deltaTime
        );
    }

    private void HandleSuspicious()
    {
        StopNavigation();
        RotateToward(lastKnownPlayerPosition);

        suspiciousTimer -= Time.deltaTime;

        if (suspiciousTimer <= 0f)
        {
            currentState = EnemyState.Idle;
        }
    }

    private void HandleIdentifying()
    {
        StopNavigation();
        RotateToward(player.position);

        playerConfirmedHostile = EvaluatePlayerIdentity();
        playerIdentityEvaluated = true;

        if (playerConfirmedHostile)
        {
            Debug.Log(
                $"{gameObject.name} identified the player as hostile."
            );

            currentState = EnemyState.Chase;
        }
        else
        {
            Debug.Log(
                $"{gameObject.name} identified the player as non-hostile."
            );

            currentState = EnemyState.Idle;
        }
    }

    private bool EvaluatePlayerIdentity()
    {
        // v0.1.0: remain for later updates
        return true;
    }

    private void HandleChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        if (distance <= attackDistance)
        {
            StopNavigation();
            currentState = EnemyState.Attack;
            return;
        }

        if (navMeshAgent == null)
        {
            Debug.LogError(
                $"{gameObject.name} has no NavMeshAgent component."
            );
            return;
        }

        if (!navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            if (!navMeshWarningShown)
            {
                Debug.LogWarning(
                    $"{gameObject.name} is not placed on a valid NavMesh."
                );

                navMeshWarningShown = true;
            }

            return;
        }

        navMeshWarningShown = false;

        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.stoppingDistance = attackDistance;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(player.position);
    }

    private void HandleAttack()
    {
        StopNavigation();
        RotateToward(player.position);

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > attackDistance + 0.4f)
        {
            currentState = EnemyState.Chase;
            return;
        }

        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            attackTimer = attackCooldown;
            PerformAttack();
        }
    }

    private void RotateToward(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime
        );
    }

    private void PerformAttack()
    {
        Vector3 center = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.0f;

        Collider[] hits = Physics.OverlapSphere(
            center,
            attackRadius,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        PlayerPrototype hitPlayer = null;

        foreach (Collider hit in hits)
        {
            PlayerPrototype candidate =
                hit.GetComponentInParent<PlayerPrototype>();

            if (candidate == null)
            {
                continue;
            }

            hitPlayer = candidate;
            break;
        }

        if (hitPlayer != null)
        {
            Debug.Log(
                $"{gameObject.name} attacked and hit {hitPlayer.gameObject.name}."
            );

            hitPlayer.ReceiveHit(gameObject);
        }
        else
        {
            Debug.Log($"{gameObject.name} attacked but missed.");
        }
    }

    public void TakeDamage(int amount)
    {
        if (currentState == EnemyState.Defeated)
        {
            return;
        }

        playerIdentityEvaluated = true;
        playerConfirmedHostile = true;

        health -= amount;
        Debug.Log($"{gameObject.name} took damage. HP: {health}");

        currentState = EnemyState.Chase;
        lastKnownPlayerPosition = player.position;

        if (health <= 0)
        {
            Defeat();
        }
    }

    private void Defeat()
    {
        currentState = EnemyState.Defeated;
        SetColor(Color.black);

        Collider collider = GetComponent<Collider>();

        if (navMeshAgent != null &&
            navMeshAgent.enabled &&
            navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }

        if (collider != null)
        {
            collider.enabled = false;
        }

        if (outpostManager != null)
        {
            outpostManager.ReportEnemyDefeated(this);
        }

        Debug.Log($"{gameObject.name} defeated.");
    }

    private void SetColor(Color color)
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = color;
        }
    }

    private void OnDrawGizmos()
    {
        // hearing area
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        // visible area
        Gizmos.color = Color.red;

        Vector3 leftRay = Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * transform.forward;
        Vector3 rightRay = Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * transform.forward;

        Gizmos.DrawRay(transform.position + Vector3.up, leftRay * visionDistance);
        Gizmos.DrawRay(transform.position + Vector3.up, rightRay * visionDistance);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * visionDistance);

        // attack area
        Vector3 attackCenter = attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.0f;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(attackCenter, attackRadius);
    }
}
