using System;
using UnityEngine;

// Dipasang pada GoalPrefab (collider harus Is Trigger).
// Saat Player menyentuh goal, event GoalReached dikirim dan gerak player dihentikan.
[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    // Didengarkan oleh DungeonGenerator untuk menampilkan pesan di HUD
    public static event Action GoalReached;

    private bool triggered;

    private void Reset()
    {
        // Pastikan collider berupa trigger saat komponen pertama kali dipasang
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
            return;

        triggered = true;

        // Hentikan gerak player
        SimplePlayerController controller =
            other.GetComponentInParent<SimplePlayerController>();

        if (controller != null)
        {
            controller.enabled = false;
        }

        Debug.Log("Goal tercapai");

        GoalReached?.Invoke();
    }
}
