using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttackTest : MonoBehaviour
{
    [SerializeField] private EnemyHealth enemy;
    [SerializeField] private float attackDistance = 3f;
    [SerializeField] private float damage = 20f;

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            TryAttackEnemy();
        }
    }

    private void TryAttackEnemy()
    {
        if (enemy == null)
            return;

        float distance = Vector3.Distance(
            transform.position,
            enemy.transform.position
        );

        if (distance <= attackDistance)
        {
            enemy.TakeDamage(damage);

            Debug.Log("Player attacks Enemy!");
        }
    }
}