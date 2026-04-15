using UnityEngine;
using UnityEngine.SceneManagement;

public class ZonePerdu : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Perdu !");
            // Exemple : recharger la scène
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}