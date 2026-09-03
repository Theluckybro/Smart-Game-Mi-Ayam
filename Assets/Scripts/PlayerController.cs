using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float _move_speed = 5f;

    void Update()
    {
        MovePlayer();
    }

    void MovePlayer()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed)
        {
            horizontal = -1f;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            horizontal = 1f;
        }
        if (Keyboard.current.wKey.isPressed)
        {
            vertical = 1f;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            vertical = -1f;
        }

        Vector3 Movement = new Vector3(horizontal, 0, vertical);

        Movement = Movement.normalized;

        transform.position += Movement * _move_speed * Time.deltaTime;

    }
}
