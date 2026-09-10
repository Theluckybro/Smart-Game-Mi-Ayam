using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 10f;

    private void Update()
    {
        MovePlayer();
    }

    private void MovePlayer()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 movement = new Vector3(
            horizontal,
            0f,
            vertical
        ).normalized;

        // Gerakkan Player
        transform.position +=
            movement *
            moveSpeed *
            Time.deltaTime;

        // Putar Player mengikuti arah gerakan
        if (movement != Vector3.zero)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(movement);

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
        }
    }
}
