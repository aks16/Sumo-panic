using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        // On trouve automatiquement la caméra principale de ton jeu
        cam = Camera.main;
    }

    // LateUpdate est utilisé pour s'assurer que la caméra a fini de bouger avant de tourner le sprite
    void LateUpdate()
    {
        if (cam != null)
        {
            // Force le Rhinocéros à s'aligner parfaitement face à la caméra
            transform.forward = cam.transform.forward;
        }
    }
}
