using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class RoundControlUI : MonoBehaviour
{
    [Header("Serveur")]
    public string serverBaseUrl = "http://localhost:3000"; // à adapter en prod

    [Header("UI")]
    public TMP_Text centerStatusText;   // Référence vers CenterStatusText
    public Button startButton;          // Bouton "Lancer la manche"

    private bool isStarting = false;

    void Awake()
    {
        if (startButton == null)
        {
            startButton = GetComponent<Button>();
        }

        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartClicked);
        }
    }

    void Update()
    {
        // Lancer avec la barre espace aussi (pratique pour le prof)
        if (!isStarting && Input.GetKeyDown(KeyCode.Space))
        {
            OnStartClicked();
        }
    }

    void OnStartClicked()
    {
        if (!isStarting)
        {
            StartCoroutine(StartRoundCoroutine());
        }
    }

    IEnumerator StartRoundCoroutine()
    {
        isStarting = true;

        // Dès qu'on lance la manche, on efface le message central ("Prêt à jouer")
        if (centerStatusText != null)
        {
            centerStatusText.text = "";
        }

        string url = serverBaseUrl + "/api/startRound";
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Manche démarrée : " + www.downloadHandler.text);
                // On ne met RIEN dans centerStatusText ici -> pas de "La manche est en cours !"
            }
            else
            {
                Debug.LogWarning("Erreur lors du démarrage de la manche : " + www.error);
                if (centerStatusText != null)
                {
                    centerStatusText.text = "Erreur de lancement de la manche";
                }
            }
        }

        // Masquer complètement le bouton après lancement
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        isStarting = false;
    }
}