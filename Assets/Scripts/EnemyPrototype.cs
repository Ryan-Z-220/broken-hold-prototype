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
        Searching,
        Defeated
    }

    [Header("References")]
    public Transform player;
    public PlayerPrototype playerNoise;
    public OutpostStateManager outpostManager;

    [Header("Perception")]
    public float hearingRadius = 4.0f;
    public float visionDistance = 8.0f;
    public float visionAngle = 80.0f;
    public float suspiciousDuration = 2.0f;
    public float sightLossGraceDuration = 0.6f;

    [Header("Movement")]
    public float chaseSpeed = 2.8f;
    public float navigationAngularSpeed = 720.0f;
    public float manualTurnSpeed = 540.0f;
    public float attackDistance = 1.5f;

    [Header("Search")]
    public float searchDuration = 3.0f;
    public float searchArrivalThreshold = 0.25f;
    public float searchTurnSpeed = 120.0f;

    [Header("Combat Awareness")]
    public float meleeAwarenessDuration = 1.25f;

    [Header("Combat")]
    public Transform attackPoint;
    public float attackRadius = 1.0f;
    public int health = 2;
    public float attackCooldown = 1.5f;

    [Header("Debug")]
    public EnemyState currentState = EnemyState.Idle;

    private Quaternion initialRotation;
    private NavMeshAgent navMeshAgent;
    private Renderer enemyRenderer;
    private bool navMeshWarningShown;

    private bool playerIdentityEvaluated;
    private bool playerConfirmedHostile;

    private Vector3 lastSeenPlayerPosition;
    private Vector3 lastHeardPlayerPosition;
    private Vector3 pursuitTargetPosition;
    private Vector3 searchTargetPosition;

    private bool hasLastSeenPlayerPosition;
    private bool hasLastHeardPlayerPosition;
    private bool hasSearchTargetPosition;
    private bool reachedSearchTargetPosition;
    private bool hasDirectCombatTarget;

    private float suspiciousTimer;
    private float attackTimer;
    private float searchTimer;
    private float sightLossTimer;
    private float meleeAwarenessTimer;

    private void Awake()
    {
        enemyRenderer = GetComponent<Renderer>();
        navMeshAgent = GetComponent<NavMeshAgent>();
        initialRotation = transform.rotation;

        if (navMeshAgent != null)
        {
            navMeshAgent.angularSpeed = navigationAngularSpeed;
        }
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
        if (
            currentState == EnemyState.Defeated ||
            player == null ||
            playerNoise == null
        )
        {
            return;
        }

        TickAwarenessTimers();

        bool sawPlayer = CanSeePlayer();
        bool heardActivePlayer = CanHearActivePlayer();

        UpdatePerceptionState(sawPlayer, heardActivePlayer);
        ExecuteCurrentState();
    }

    private void TickAwarenessTimers()
    {
        if (meleeAwarenessTimer > 0.0f)
        {
            meleeAwarenessTimer -= Time.deltaTime;
        }
    }

    private void UpdatePerceptionState(
        bool sawPlayer,
        bool heardActivePlayer
    )
    {
        hasDirectCombatTarget = false;

        if (sawPlayer)
        {
            RecordVisualContact();
            sightLossTimer = 0.0f;
            hasDirectCombatTarget = true;

            if (!playerIdentityEvaluated)
            {
                currentState = EnemyState.Identifying;
                return;
            }

            if (
                playerConfirmedHostile &&
                currentState != EnemyState.Attack
            )
            {
                currentState = EnemyState.Chase;
            }

            return;
        }

        if (
            playerConfirmedHostile &&
            meleeAwarenessTimer > 0.0f
        )
        {
            pursuitTargetPosition = player.position;
            searchTargetPosition = player.position;
            hasSearchTargetPosition = true;
            sightLossTimer = 0.0f;
            hasDirectCombatTarget = true;

            if (currentState != EnemyState.Attack)
            {
                currentState = EnemyState.Chase;
            }

            return;
        }

        if (heardActivePlayer)
        {
            RecordSoundContact();
            sightLossTimer = 0.0f;

            if (playerConfirmedHostile)
            {
                // Sound provides a location clue, not direct visual confirmation.
                // A previously confirmed hostile target is investigated immediately
                // without returning to the identification step.
                UpdateSearchTargetFromSound();
            }
            else
            {
                EnterSuspicious(lastHeardPlayerPosition);
            }

            return;
        }

        bool lostAllCombatClues =
            playerConfirmedHostile &&
            (
                currentState == EnemyState.Chase ||
                currentState == EnemyState.Attack
            );

        if (!lostAllCombatClues)
        {
            return;
        }

        sightLossTimer += Time.deltaTime;

        if (currentState == EnemyState.Attack)
        {
            currentState = EnemyState.Chase;
        }

        if (sightLossTimer >= sightLossGraceDuration)
        {
            EnterSearching(GetBestSearchPosition());
        }
    }

    private void ExecuteCurrentState()
    {
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
                SetColor(new Color(1.0f, 0.2f, 0.2f));
                HandleAttack();
                break;

            case EnemyState.Searching:
                SetColor(new Color(1.0f, 0.5f, 0.0f));
                HandleSearching();
                break;
        }
    }

    private bool CanHearActivePlayer()
    {
        if (!playerNoise.IsMakingActiveNoise)
        {
            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // The sound is detected when the player's noise area intersects
        // the enemy's hearing area.
        return distance <=
            hearingRadius + playerNoise.CurrentNoiseRadius;
    }

    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0.0f;

        if (toPlayer.magnitude > visionDistance)
        {
            return false;
        }

        if (toPlayer.sqrMagnitude < 0.001f)
        {
            return true;
        }

        float angle = Vector3.Angle(
            transform.forward,
            toPlayer.normalized
        );

        if (angle > visionAngle * 0.5f)
        {
            return false;
        }

        Vector3 eyePosition =
            transform.position + Vector3.up * 1.2f;
        Vector3 targetPosition =
            player.position + Vector3.up * 1.0f;
        Vector3 direction = targetPosition - eyePosition;

        if (
            Physics.Raycast(
                eyePosition,
                direction.normalized,
                out RaycastHit hit,
                visionDistance
            )
        )
        {
            PlayerPrototype visiblePlayer =
                hit.collider.GetComponentInParent<PlayerPrototype>();

            return visiblePlayer == playerNoise;
        }

        return false;
    }

    private void RecordVisualContact()
    {
        lastSeenPlayerPosition = player.position;
        pursuitTargetPosition = player.position;
        searchTargetPosition = player.position;

        hasLastSeenPlayerPosition = true;
        hasSearchTargetPosition = true;
    }

    private void RecordSoundContact()
    {
        // Record the sound before changing the active search target.
        // UpdateSearchTargetFromSound() compares the old and new positions
        // to determine whether navigation must resume.
        lastHeardPlayerPosition = player.position;
        hasLastHeardPlayerPosition = true;
    }

    private void EnterSuspicious(Vector3 soundPosition)
    {
        lastHeardPlayerPosition = soundPosition;
        hasLastHeardPlayerPosition = true;
        suspiciousTimer = suspiciousDuration;
        currentState = EnemyState.Suspicious;
    }

    private bool EvaluatePlayerIdentity()
    {
        // v0.1.1b placeholder: faction and disguise logic will be added later.
        return true;
    }

    private void EnterSearching(Vector3 targetPosition)
    {
        searchTargetPosition = targetPosition;
        hasSearchTargetPosition = true;
        reachedSearchTargetPosition = false;
        searchTimer = searchDuration;
        currentState = EnemyState.Searching;
    }

    private void UpdateSearchTargetFromSound()
    {
        bool wasSearching = currentState == EnemyState.Searching;

        bool targetChanged =
            !hasSearchTargetPosition ||
            Vector3.SqrMagnitude(
                searchTargetPosition - lastHeardPlayerPosition
            ) > 0.04f;

        searchTargetPosition = lastHeardPlayerPosition;
        hasSearchTargetPosition = true;
        searchTimer = searchDuration;

        // Resume navigation when a new sound is heard after arriving,
        // or when the sound source has moved to a different location.
        if (
            !wasSearching ||
            targetChanged ||
            reachedSearchTargetPosition
        )
        {
            reachedSearchTargetPosition = false;
        }

        currentState = EnemyState.Searching;
    }

    private Vector3 GetBestSearchPosition()
    {
        if (hasSearchTargetPosition)
        {
            return searchTargetPosition;
        }

        if (hasLastSeenPlayerPosition)
        {
            return lastSeenPlayerPosition;
        }

        if (hasLastHeardPlayerPosition)
        {
            return lastHeardPlayerPosition;
        }

        return transform.position;
    }

    private void StopNavigation()
    {
        if (!IsNavigationReady())
        {
            return;
        }

        navMeshAgent.isStopped = true;
    }

    private bool IsNavigationReady()
    {
        if (navMeshAgent == null)
        {
            if (!navMeshWarningShown)
            {
                Debug.LogError(
                    $"{gameObject.name} has no NavMeshAgent component."
                );
                navMeshWarningShown = true;
            }

            return false;
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

            return false;
        }

        navMeshWarningShown = false;
        return true;
    }

    private void MoveTo(
        Vector3 destination,
        float stoppingDistance
    )
    {
        if (!IsNavigationReady())
        {
            return;
        }

        navMeshAgent.speed = chaseSpeed;
        navMeshAgent.angularSpeed = navigationAngularSpeed;
        navMeshAgent.stoppingDistance = stoppingDistance;
        navMeshAgent.isStopped = false;
        navMeshAgent.SetDestination(destination);
    }

    private void HandleIdle()
    {
        StopNavigation();

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            initialRotation,
            manualTurnSpeed * Time.deltaTime
        );
    }

    private void HandleSuspicious()
    {
        StopNavigation();
        RotateToward(lastHeardPlayerPosition);

        suspiciousTimer -= Time.deltaTime;

        if (suspiciousTimer <= 0.0f)
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

            pursuitTargetPosition = player.position;
            searchTargetPosition = player.position;
            hasSearchTargetPosition = true;
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

    private void HandleChase()
    {
        if (hasDirectCombatTarget)
        {
            float playerDistance = Vector3.Distance(
                transform.position,
                player.position
            );

            if (playerDistance <= attackDistance)
            {
                StopNavigation();
                currentState = EnemyState.Attack;
                return;
            }
        }

        float stoppingDistance = hasDirectCombatTarget
            ? attackDistance
            : searchArrivalThreshold;

        MoveTo(pursuitTargetPosition, stoppingDistance);
    }

    private void HandleAttack()
    {
        StopNavigation();

        if (!hasDirectCombatTarget)
        {
            currentState = EnemyState.Chase;
            return;
        }

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

        if (attackTimer <= 0.0f)
        {
            attackTimer = attackCooldown;
            PerformAttack();
        }
    }

    private void HandleSearching()
    {
        if (!hasSearchTargetPosition)
        {
            currentState = EnemyState.Idle;
            return;
        }

        if (!IsNavigationReady())
        {
            return;
        }

        if (!reachedSearchTargetPosition)
        {
            MoveTo(
                searchTargetPosition,
                searchArrivalThreshold
            );

            bool reachedDestination =
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <=
                    navMeshAgent.stoppingDistance + 0.05f;

            if (reachedDestination)
            {
                reachedSearchTargetPosition = true;
                StopNavigation();

                Debug.Log(
                    $"{gameObject.name} reached the search location."
                );
            }

            return;
        }

        StopNavigation();

        // A simple in-place search until patrol or routine behavior is added.
        transform.Rotate(
            Vector3.up,
            searchTurnSpeed * Time.deltaTime
        );

        searchTimer -= Time.deltaTime;

        if (searchTimer <= 0.0f)
        {
            hasSearchTargetPosition = false;
            currentState = EnemyState.Idle;

            Debug.Log(
                $"{gameObject.name} could not find the player and returned to Idle."
            );
        }
    }

    private void RotateToward(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0.0f;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            manualTurnSpeed * Time.deltaTime
        );
    }

    private Vector3 GetAttackCenter()
    {
        return attackPoint != null
            ? attackPoint.position
            : transform.position + transform.forward * 1.0f;
    }

    private void PerformAttack()
    {
        Collider[] hits = Physics.OverlapSphere(
            GetAttackCenter(),
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
            Debug.Log(
                $"{gameObject.name} attacked but missed."
            );
        }
    }

    public void TakeDamage(int amount)
    {
        TakeDamage(amount, null);
    }

    public void TakeDamage(int amount, Transform attacker)
    {
        if (currentState == EnemyState.Defeated)
        {
            return;
        }

        health -= amount;
        Debug.Log(
            $"{gameObject.name} took damage. HP: {health}"
        );

        if (health <= 0)
        {
            Defeat();
            return;
        }

        playerIdentityEvaluated = true;
        playerConfirmedHostile = true;
        sightLossTimer = 0.0f;
        meleeAwarenessTimer = meleeAwarenessDuration;

        if (attacker != null)
        {
            PlayerPrototype attackerPlayer =
                attacker.GetComponentInParent<PlayerPrototype>();

            if (attackerPlayer != null)
            {
                player = attackerPlayer.transform;
                playerNoise = attackerPlayer;
            }

            pursuitTargetPosition = attacker.position;
            searchTargetPosition = attacker.position;
            hasSearchTargetPosition = true;
            hasDirectCombatTarget = true;
        }
        else if (player != null)
        {
            pursuitTargetPosition = player.position;
            searchTargetPosition = player.position;
            hasSearchTargetPosition = true;
            hasDirectCombatTarget = true;
        }

        float distanceToAttacker = player != null
            ? Vector3.Distance(transform.position, player.position)
            : float.MaxValue;

        currentState = distanceToAttacker <= attackDistance
            ? EnemyState.Attack
            : EnemyState.Chase;
    }

    private void Defeat()
    {
        currentState = EnemyState.Defeated;
        SetColor(Color.black);

        if (
            navMeshAgent != null &&
            navMeshAgent.enabled &&
            navMeshAgent.isOnNavMesh
        )
        {
            navMeshAgent.isStopped = true;
            navMeshAgent.ResetPath();
            navMeshAgent.enabled = false;
        }

        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider enemyCollider in colliders)
        {
            enemyCollider.enabled = false;
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
        // Hearing area.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, hearingRadius);

        // Vision cone.
        Gizmos.color = Color.red;

        Vector3 leftRay =
            Quaternion.Euler(0.0f, -visionAngle * 0.5f, 0.0f) *
            transform.forward;
        Vector3 rightRay =
            Quaternion.Euler(0.0f, visionAngle * 0.5f, 0.0f) *
            transform.forward;

        Vector3 eyePosition = transform.position + Vector3.up;

        Gizmos.DrawRay(eyePosition, leftRay * visionDistance);
        Gizmos.DrawRay(eyePosition, rightRay * visionDistance);
        Gizmos.DrawRay(
            eyePosition,
            transform.forward * visionDistance
        );

        // Attack area.
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(GetAttackCenter(), attackRadius);

        if (hasLastSeenPlayerPosition)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                lastSeenPlayerPosition + Vector3.up * 0.2f,
                0.25f
            );
        }

        if (hasLastHeardPlayerPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(
                lastHeardPlayerPosition + Vector3.up * 0.2f,
                0.2f
            );
        }

        if (hasSearchTargetPosition)
        {
            Gizmos.color = new Color(1.0f, 0.5f, 0.0f);
            Vector3 markerPosition =
                searchTargetPosition + Vector3.up * 0.2f;

            Gizmos.DrawWireSphere(markerPosition, 0.3f);
            Gizmos.DrawLine(
                transform.position + Vector3.up * 0.2f,
                markerPosition
            );
        }
    }
}
