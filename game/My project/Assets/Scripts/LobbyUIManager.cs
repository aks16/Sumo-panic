using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

/// <summary>
/// Gere l'ecran de lobby avec le QR Code, le compteur de joueurs,
/// et la transition vers le jeu.
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [Header("Configuration")]
    public int serverPort = 3000;
    public string serverPath = "/play";

    [Header("UI Elements - Titre")]
    public TMP_Text titleText;
    public TMP_Text subtitleText;

    [Header("UI Elements - QR Code")]
    public RawImage qrCodeImage;
    public TMP_Text scanInstructionText;
    public TMP_Text urlText;

    [Header("UI Elements - Joueurs")]
    public TMP_Text playerCountText;
    public TMP_Text waitingText;

    [Header("UI Elements - Instructions")]
    public GameObject instructionsPanel;
    public TMP_Text instructionsText;

    [Header("UI Elements - Boutons")]
    public Button startButton;
    public TMP_Text startButtonText;

    [Header("Animation Sumo (Optionnel)")]
    public Animator sumoAnimator;
    public GameObject sumoChargeEffect;

    [Header("Compte a rebours")]
    public TMP_Text countdownText;
    public float countdownDuration = 3f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip countdownBeep;
    public AudioClip goSound;

    [Header("Nombre minimum de joueurs")]
    public int minPlayersToStart = 1;

    private string serverUrl;
    private int currentPlayerCount = 0;
    private bool isStarting = false;
    private Texture2D qrTexture;

    void Start()
    {
        // Generer l'URL du serveur
        serverUrl = GetLocalIPAddress();
        
        // Generer le QR Code
        GenerateQRCode();
        
        // Afficher l'URL
        if (urlText != null)
        {
            urlText.text = $"http://{serverUrl}:{serverPort}{serverPath}";
        }

        // Configurer le bouton start
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
            startButton.interactable = false; // Desactive jusqu'a avoir assez de joueurs
        }

        // Cacher le compte a rebours
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        // Demarrer le polling du nombre de joueurs
        StartCoroutine(PollPlayerCount());

        // Configurer les textes
        SetupTexts();
    }

    void SetupTexts()
    {
        if (titleText != null)
        {
            titleText.text = "SUMO PANIC ARENA";
        }

        if (subtitleText != null)
        {
            subtitleText.text = "Le dernier debout gagne!";
        }

        if (scanInstructionText != null)
        {
            scanInstructionText.text = "Scannez le QR Code pour rejoindre";
        }

        if (waitingText != null)
        {
            waitingText.text = "En attente de joueurs...";
        }

        if (instructionsText != null)
        {
            instructionsText.text = 
                "REGLES DU JEU\n\n" +
                "Utilisez le joystick pour vous deplacer\n" +
                "Appuyez sur POUSSER pour repousser vos adversaires\n" +
                "Restez sur la plateforme!\n" +
                "Le sol s'effondre progressivement\n" +
                "Attention au RHINOCEROS!\n\n" +
                "Le dernier sumo debout remporte la manche!";
        }

        UpdateStartButtonText();
    }

    string GetLocalIPAddress()
    {
        string localIP = "localhost";
        
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                if (endPoint != null)
                {
                    localIP = endPoint.Address.ToString();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Impossible d'obtenir l'IP locale: " + e.Message);
            
            // Fallback: chercher dans les interfaces reseau
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
        }

        return localIP;
    }

    void GenerateQRCode()
    {
        if (qrCodeImage == null) return;

        string url = $"http://{serverUrl}:{serverPort}{serverPath}";
        
        // Utiliser une API externe pour generer le QR code
        // Tu peux aussi utiliser une librairie locale comme ZXing
        StartCoroutine(LoadQRCodeFromAPI(url));
    }

    IEnumerator LoadQRCodeFromAPI(string dataToEncode)
    {
        // Utiliser l'API QR Server (gratuite, pas de cle API requise)
        string encodedData = UnityWebRequest.EscapeURL(dataToEncode);
        string qrApiUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={encodedData}";

        using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(qrApiUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                qrTexture = DownloadHandlerTexture.GetContent(www);
                qrCodeImage.texture = qrTexture;
            }
            else
            {
                Debug.LogWarning("Erreur generation QR Code: " + www.error);
                // Fallback: afficher juste l'URL
                if (scanInstructionText != null)
                {
                    scanInstructionText.text = $"Allez sur:\n{dataToEncode}";
                }
            }
        }
    }

    IEnumerator PollPlayerCount()
    {
        while (true)
        {
            string url = $"http://localhost:{serverPort}/api/playerCount";
            
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    if (int.TryParse(www.downloadHandler.text, out int count))
                    {
                        UpdatePlayerCount(count);
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    void UpdatePlayerCount(int count)
    {
        currentPlayerCount = count;

        if (playerCountText != null)
        {
            playerCountText.text = $"Joueurs connectes: {count}";
        }

        if (waitingText != null)
        {
            if (count == 0)
            {
                waitingText.text = "En attente de joueurs...";
            }
            else if (count < minPlayersToStart)
            {
                waitingText.text = $"Il faut au moins {minPlayersToStart} joueur(s)";
            }
            else
            {
                waitingText.text = "Pret a jouer!";
            }
        }

        // Activer/desactiver le bouton start
        if (startButton != null && !isStarting)
        {
            startButton.interactable = count >= minPlayersToStart;
        }

        UpdateStartButtonText();
    }

    void UpdateStartButtonText()
    {
        if (startButtonText != null)
        {
            if (currentPlayerCount < minPlayersToStart)
            {
                startButtonText.text = $"EN ATTENTE ({currentPlayerCount}/{minPlayersToStart})";
            }
            else
            {
                startButtonText.text = "COMMENCER!";
            }
        }
    }

    void OnStartButtonClicked()
    {
        if (isStarting) return;
        if (currentPlayerCount < minPlayersToStart) return;

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        isStarting = true;
        
        if (startButton != null)
        {
            startButton.interactable = false;
        }

        // Animation du sumo qui charge (optionnel)
        if (sumoAnimator != null)
        {
            sumoAnimator.SetTrigger("Charge");
        }

        if (sumoChargeEffect != null)
        {
            sumoChargeEffect.SetActive(true);
        }

        yield return new WaitForSeconds(1f);

        // Cacher les elements du lobby
        if (qrCodeImage != null) qrCodeImage.gameObject.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        if (startButton != null) startButton.gameObject.SetActive(false);
        if (scanInstructionText != null) scanInstructionText.gameObject.SetActive(false);
        if (urlText != null) urlText.gameObject.SetActive(false);
        if (waitingText != null) waitingText.gameObject.SetActive(false);

        // Compte a rebours
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);

            for (int i = (int)countdownDuration; i > 0; i--)
            {
                countdownText.text = i.ToString();
                
                if (audioSource != null && countdownBeep != null)
                {
                    audioSource.PlayOneShot(countdownBeep);
                }

                yield return new WaitForSeconds(1f);
            }

            countdownText.text = "SUMO!";
            
            if (audioSource != null && goSound != null)
            {
                audioSource.PlayOneShot(goSound);
            }

            yield return new WaitForSeconds(0.5f);
        }

        // Notifier le serveur et demarrer le jeu
        yield return StartCoroutine(NotifyServerAndStart());
    }

    IEnumerator NotifyServerAndStart()
    {
        string url = $"http://localhost:{serverPort}/api/startRound";
        
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Manche demarree!");
            }
            else
            {
                Debug.LogWarning("Erreur demarrage: " + www.error);
            }
        }

        // Desactiver cet ecran et activer la scene de jeu
        // Option 1: Changer de scene
        // UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        
        // Option 2: Activer/desactiver des GameObjects
        gameObject.SetActive(false);
        
        // Si tu as un GameManager, l'informer
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPhase(GameManager.GamePhase.Playing);
        }
    }

    void OnDestroy()
    {
        if (qrTexture != null)
        {
            Destroy(qrTexture);
        }
    }
}
