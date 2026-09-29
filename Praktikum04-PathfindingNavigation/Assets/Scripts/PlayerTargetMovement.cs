using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerTargetMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private void Update()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                input.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                input.x += 1f;

            if (Keyboard.current.sKey.isPressed)
                input.y -= 1f;

            if (Keyboard.current.wKey.isPressed)
                input.y += 1f;
        }

        Vector3 direction = new Vector3(
            input.x,
            0f,
            input.y
        ).normalized;

        transform.position +=
            direction *
            moveSpeed *
            Time.deltaTime;
    }
}
