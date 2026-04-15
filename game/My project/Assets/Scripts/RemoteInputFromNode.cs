using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class RemoteInputFromNode : MonoBehaviour
{
    [System.Serializable]
    private class InputDto
    {
        public float h;
        public float v;
        public bool push;
    }

    // URL de l'API Node qui renvoie l'input
    public string serverUrl = "http://localhost:3000/api/input";

    // Valeurs accessibles par les autres scripts
    [Header("Valeurs lues depuis Node")]
    public float horizontal;
    public float vertical;
    public bool push;

    void Start()
    {
        StartCoroutine(PollLoop());
    }

    IEnumerator PollLoop()
    {
        while (true)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(serverUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string json = www.downloadHandler.text;
                    try
                    {
                        InputDto data = JsonUtility.FromJson<InputDto>(json);
                        horizontal = data.h;
                        vertical = data.v;
                        push = data.push;
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("Erreur parse JSON input : " + e.Message + " / " + json);
                    }
                }
                else
                {
                    // Optionnel : log si besoin
                    // Debug.LogWarning("Erreur requête input : " + www.error);
                }
            }

            // Fréquence de mise à jour (toutes les 0.1 s)
            yield return new WaitForSeconds(0.1f);
        }
    }
}