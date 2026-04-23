using UnityEngine;

public class camera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform playerTransform;
    
    [Header("Offset Settings")]
    public Vector3 offset = new Vector3(10f, 10f, -10f); 
    
    [Header("Smoothing")]
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (playerTransform == null) return;

        Vector3 desiredPosition = new Vector3(
            playerTransform.position.x + offset.x,
            offset.y,
            playerTransform.position.z + offset.z
        );

        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
    }
}