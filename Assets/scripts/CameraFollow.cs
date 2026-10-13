using UnityEngine;

/// <summary>
/// La camera suit le joueur avec un leger retard, pour un mouvement doux.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform player;
    public float smoothSpeed = 0.125f;                      // 0 = immobile, 1 = colle au joueur
    public Vector3 offset = new Vector3(0, 0, -10);         // z = -10 : la camera reste devant la scene 2D

    // LateUpdate : apres le deplacement du joueur, donc pas de tremblement
    void LateUpdate()
    {
        if (player == null) return;

        Vector3 desiredPosition = player.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }
}
