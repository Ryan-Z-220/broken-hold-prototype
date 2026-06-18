using UnityEngine;

public class OutpostStateManager : MonoBehaviour
{
    public enum OutpostState
    {
        Hostile,
        Cleared
    }

    [Header("Outpost State")]
    public OutpostState currentState = OutpostState.Hostile;

    [Header("Scene References")]
    public EnemyPrototype[] enemies;
    public GameObject campfireObject;

    private void Start()
    {
        if (enemies == null || enemies.Length == 0)
        {
            enemies = FindObjectsOfType<EnemyPrototype>();
        }

        foreach (EnemyPrototype enemy in enemies)
        {
            enemy.outpostManager = this;
        }

        if (campfireObject != null)
        {
            campfireObject.SetActive(false);
        }

        Debug.Log("Outpost state: Hostile");
    }

    public void ReportEnemyDefeated(EnemyPrototype defeatedEnemy)
    {
        CheckIfOutpostCleared();
    }

    private void CheckIfOutpostCleared()
    {
        foreach (EnemyPrototype enemy in enemies)
        {
            if (enemy != null && enemy.currentState != EnemyPrototype.EnemyState.Defeated)
            {
                return;
            }
        }

        ClearOutpost();
    }

    private void ClearOutpost()
    {
        if (currentState == OutpostState.Cleared)
        {
            return;
        }

        currentState = OutpostState.Cleared;

        if (campfireObject != null)
        {
            campfireObject.SetActive(true);
        }

        Debug.Log("Outpost cleared. The world has changed.");
    }
}