using System.Collections;
using UnityEngine;
using UnityEngine.Networking; // pour UnityWebRequest
using TMPro;                  // pour TMP_Text

public class PlayerCountDisplay : MonoBehaviour
{
    // URL de ton serveur Node
    public string serverUrl = "http://localhost:3000/api/playerCount";

    // Référence vers le texte TMP à mettre à jour
    public TMP_Text text;

    void Start()
    {
        // Si la référence n'est pas mise dans l’Inspector, on essaie
        // de récupérer le composant texte sur le même GameObject
        if (text == null)
        {
            text = GetComponent<TMP_Text>();
        }

        StartCoroutine(UpdatePlayerCountLoop());
    }

    IEnumerator UpdatePlayerCountLoop()
    {
        while (true)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(serverUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string raw = www.downloadHandler.text; // ex : "3"
                    int count;
                    if (int.TryParse(raw, out count))
                    {
                        text.text = "Joueurs connectés : " + count;
                    }
                    else
                    {
                        text.text = "Erreur parse compteur";
                    }
                }
                else
                {
                    text.text = "Erreur connexion serveur";
                }
            }

            // On attend 1 seconde avant la prochaine requête
            yield return new WaitForSeconds(1f);
        }
    }
}