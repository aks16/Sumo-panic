using System.Collections;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Gere la sequence d'intro complete:
/// 1. Ecran titre "SUMO PANIC ARENA"
/// 2. Animation du sumo qui charge vers l'ecran
/// 3. QR Code + attente des joueurs
/// 4. Panneau d'instructions
/// 5. Compte a rebours 3-2-1
/// 6. Transition vers la scene de jeu
/// </summary>
public class IntroSequenceManager : MonoBehaviour
{
    public enum IntroPhase
    {
        Title,          // Affichage du titre
        SumoCharge,     // Animation du sumo qui charge
        QRCode,         // QR Code et attente des joueurs
        Instructions,   // Panneau d'instructions
        Countdown,      // Compte a rebours 3-2-1
        Transition      // Transition vers le jeu
    }

    [Header("Configuration Serveur")]
    public int serverPort = 3000;
    public string serverPath = "/play";

    [Header("Configuration Jeu")]
    public int minPlayersToStart = 1;
    public int maxPlayers = 8;
    public string gameSceneName = "SampleScene"; // Nom de ta scene de jeu

    [Header("=== PHASE 1: TITRE ===")]
    public CanvasGroup titlePhaseGroup;
    public TMP_Text titleText;
    public TMP_Text subtitleText;
    public Image titleBackground;
    public float titleDisplayDuration = 3f;

    [Header("=== PHASE 2: SUMO CHARGE ===")]
    public CanvasGroup sumoChargeGroup;
    public RectTransform sumoChargeImage;
    public Image impactFlash;
    public float sumoChargeDuration = 2f;
    public AnimationCurve sumoChargeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("=== PHASE 3: QR CODE ===")]
    public CanvasGroup qrCodePhaseGroup;
    public RawImage qrCodeImage;
    public TMP_Text scanInstructionText;
    public TMP_Text urlText;
    public TMP_Text playerCountText;
    public TMP_Text waitingStatusText;
    public Button startGameButton;
    public TMP_Text startButtonText;

    [Header("=== PHASE 4: INSTRUCTIONS ===")]
    public CanvasGroup instructionsPhaseGroup;
    public TMP_Text instructionsTitleText;
    public TMP_Text instructionsContentText;
    public Image instructionsPanelBg;

    [Header("=== PHASE 5: COMPTE A REBOURS ===")]
    public CanvasGroup countdownPhaseGroup;
    public TMP_Text countdownText;
    public TMP_Text countdownSubText;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip titleMusic;
    public AudioClip sumoChargeSound;
    public AudioClip impactSound;
    public AudioClip playerJoinSound;
    public AudioClip countdownBeep;
    public AudioClip goSound;

    [Header("Effets visuels")]
    public ParticleSystem impactParticles;
    public float screenShakeDuration = 0.3f;
    public float screenShakeIntensity = 10f;

    // Variables internes
    private IntroPhase currentPhase = IntroPhase.Title;
    private string serverUrl;
    private int currentPlayerCount = 0;
    private int previousPlayerCount = 0;
    private bool isTransitioning = false;
    private Texture2D qrTexture;
    private Coroutine playerPollCoroutine;
    private RectTransform canvasRect;

    void Awake()
    {
        // S'assurer qu'on a un AudioSource
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    void Start()
    {
        // Obtenir le Canvas pour les effets
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            canvasRect = canvas.GetComponent<RectTransform>();
        }

        // Obtenir l'IP locale
        serverUrl = GetLocalIPAddress();

        // Initialiser toutes les phases (tout cacher sauf le titre)
        InitializePhases();

        // Demarrer la sequence
        StartCoroutine(RunIntroSequence());
    }

    void InitializePhases()
    {
        // Cacher toutes les phases
        SetCanvasGroupVisible(titlePhaseGroup, false);
        SetCanvasGroupVisible(sumoChargeGroup, false);
        SetCanvasGroupVisible(qrCodePhaseGroup, false);
        SetCanvasGroupVisible(instructionsPhaseGroup, false);
        SetCanvasGroupVisible(countdownPhaseGroup, false);

        // Cacher le flash d'impact
        if (impactFlash != null)
        {
            Color c = impactFlash.color;
            c.a = 0;
            impactFlash.color = c;
        }

        // Configurer le bouton start
        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(OnStartButtonClicked);
            startGameButton.interactable = false;
        }

        // Configurer les textes
        SetupTexts();
    }

    void SetupTexts()
    {
        if (titleText != null)
            titleText.text = "SUMO PANIC\nARENA";

        if (subtitleText != null)
            subtitleText.text = "Le dernier debout gagne!";

        if (scanInstructionText != null)
            scanInstructionText.text = "SCANNEZ LE QR CODE\nPOUR REJOINDRE LA PARTIE";

        if (instructionsTitleText != null)
            instructionsTitleText.text = "COMMENT JOUER";

        if (instructionsContentText != null)
        {
            instructionsContentText.text =
                "<sprite=0> Utilisez le JOYSTICK pour vous deplacer\n\n" +
                "<sprite=1> Appuyez sur POUSSER pour repousser vos adversaires\n\n" +
                "<sprite=2> Restez sur la plateforme - Le sol s'effondre!\n\n" +
                "<sprite=3> Attention au RHINOCEROS aleatoire!\n\n" +
                "<size=120%><b>LE DERNIER SUMO DEBOUT GAGNE!</b></size>";
        }

        if (countdownSubText != null)
            countdownSubText.text = "PREPAREZ-VOUS!";
    }

    IEnumerator RunIntroSequence()
    {
        // ===== PHASE 1: TITRE =====
        currentPhase = IntroPhase.Title;
        yield return StartCoroutine(PlayTitlePhase());

        // ===== PHASE 2: SUMO CHARGE =====
        currentPhase = IntroPhase.SumoCharge;
        yield return StartCoroutine(PlaySumoChargePhase());

        // ===== PHASE 3: QR CODE ET ATTENTE =====
        currentPhase = IntroPhase.QRCode;
        yield return StartCoroutine(PlayQRCodePhase());

        // Les phases suivantes sont declenchees par le bouton ou automatiquement
    }

    // ==================== PHASE 1: TITRE ====================
    IEnumerator PlayTitlePhase()
    {
        // Jouer la musique de titre
        if (audioSource != null && titleMusic != null)
        {
            audioSource.clip = titleMusic;
            audioSource.loop = true;
            audioSource.Play();
        }

        // Fade in du titre
        yield return StartCoroutine(FadeCanvasGroup(titlePhaseGroup, 0, 1, 0.5f));

        // Animation du texte (pulsation)
        float elapsed = 0;
        while (elapsed < titleDisplayDuration)
        {
            if (titleText != null)
            {
                float scale = 1f + Mathf.Sin(elapsed * 3f) * 0.05f;
                titleText.transform.localScale = Vector3.one * scale;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fade out du titre
        yield return StartCoroutine(FadeCanvasGroup(titlePhaseGroup, 1, 0, 0.3f));
    }

    // ==================== PHASE 2: SUMO CHARGE ====================
    IEnumerator PlaySumoChargePhase()
    {
        SetCanvasGroupVisible(sumoChargeGroup, true);

        // Position initiale du sumo (hors ecran a gauche)
        if (sumoChargeImage != null)
        {
            Vector2 startPos = new Vector2(-1500, 0);
            Vector2 endPos = new Vector2(0, 0);
            sumoChargeImage.anchoredPosition = startPos;

            // Jouer le son de charge
            if (audioSource != null && sumoChargeSound != null)
            {
                audioSource.PlayOneShot(sumoChargeSound);
            }

            // Animation de charge
            float elapsed = 0;
            while (elapsed < sumoChargeDuration)
            {
                float t = sumoChargeCurve.Evaluate(elapsed / sumoChargeDuration);
                sumoChargeImage.anchoredPosition = Vector2.Lerp(startPos, endPos, t);

                // Faire trembler legerement pendant la course
                if (elapsed > sumoChargeDuration * 0.3f)
                {
                    float shake = Mathf.Sin(elapsed * 50) * 5 * (1 - t);
                    sumoChargeImage.anchoredPosition += new Vector2(0, shake);
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            sumoChargeImage.anchoredPosition = endPos;
        }

        // IMPACT!
        yield return StartCoroutine(PlayImpactEffect());

        // Fade out de la phase sumo
        yield return StartCoroutine(FadeCanvasGroup(sumoChargeGroup, 1, 0, 0.2f));
    }

    IEnumerator PlayImpactEffect()
    {
        // Flash blanc
        if (impactFlash != null)
        {
            Color flashColor = Color.white;
            flashColor.a = 1;
            impactFlash.color = flashColor;

            // Jouer le son d'impact
            if (audioSource != null && impactSound != null)
            {
                audioSource.PlayOneShot(impactSound);
            }

            // Particules
            if (impactParticles != null)
            {
                impactParticles.Play();
            }

            // Screen shake
            yield return StartCoroutine(ScreenShake());

            // Fade out du flash
            float elapsed = 0;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                flashColor.a = Mathf.Lerp(1, 0, elapsed / duration);
                impactFlash.color = flashColor;
                elapsed += Time.deltaTime;
                yield return null;
            }

            flashColor.a = 0;
            impactFlash.color = flashColor;
        }
    }

    IEnumerator ScreenShake()
    {
        if (canvasRect == null) yield break;

        Vector2 originalPos = canvasRect.anchoredPosition;
        float elapsed = 0;

        while (elapsed < screenShakeDuration)
        {
            float x = Random.Range(-1f, 1f) * screenShakeIntensity;
            float y = Random.Range(-1f, 1f) * screenShakeIntensity;

            canvasRect.anchoredPosition = originalPos + new Vector2(x, y);

            elapsed += Time.deltaTime;
            yield return null;
        }

        canvasRect.anchoredPosition = originalPos;
    }

    // ==================== PHASE 3: QR CODE ====================
    IEnumerator PlayQRCodePhase()
    {
        // Generer le QR Code
        StartCoroutine(GenerateQRCode());

        // Afficher l'URL
        if (urlText != null)
        {
            urlText.text = $"http://{serverUrl}:{serverPort}{serverPath}";
        }

        // Fade in du QR Code
        yield return StartCoroutine(FadeCanvasGroup(qrCodePhaseGroup, 0, 1, 0.5f));

        // Demarrer le polling des joueurs
        playerPollCoroutine = StartCoroutine(PollPlayerCount());

        // Attendre que le bouton soit clique ou qu'il y ait assez de joueurs
        // (La suite est geree par OnStartButtonClicked)
    }

    IEnumerator GenerateQRCode()
    {
        if (qrCodeImage == null) yield break;

        string url = $"http://{serverUrl}:{serverPort}{serverPath}";
        string encodedData = UnityWebRequest.EscapeURL(url);
        string qrApiUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={encodedData}&bgcolor=FFFFFF&color=2D1B14";

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
                if (scanInstructionText != null)
                {
                    scanInstructionText.text = $"ALLEZ SUR:\n{url}";
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
                www.timeout = 2;
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    // Essayer de parser comme JSON ou comme nombre simple
                    string response = www.downloadHandler.text;
                    
                    // Si c'est du JSON
                    if (response.StartsWith("{"))
                    {
                        PlayerCountResponse pcr = JsonUtility.FromJson<PlayerCountResponse>(response);
                        if (pcr != null)
                        {
                            UpdatePlayerCount(pcr.count);
                        }
                    }
                    else if (int.TryParse(response, out int count))
                    {
                        UpdatePlayerCount(count);
                    }
                }
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    [System.Serializable]
    private class PlayerCountResponse
    {
        public int count;
    }

    void UpdatePlayerCount(int count)
    {
        previousPlayerCount = currentPlayerCount;
        currentPlayerCount = count;

        // Jouer un son si un nouveau joueur rejoint
        if (count > previousPlayerCount && audioSource != null && playerJoinSound != null)
        {
            audioSource.PlayOneShot(playerJoinSound);
        }

        // Mettre a jour l'UI
        if (playerCountText != null)
        {
            playerCountText.text = $"JOUEURS: {count}/{maxPlayers}";
        }

        if (waitingStatusText != null)
        {
            if (count == 0)
            {
                waitingStatusText.text = "En attente de joueurs...";
            }
            else if (count < minPlayersToStart)
            {
                waitingStatusText.text = $"Il faut au moins {minPlayersToStart} joueur(s) pour commencer";
            }
            else
            {
                waitingStatusText.text = "Pret a jouer! Cliquez sur COMMENCER";
            }
        }

        // Activer/desactiver le bouton
        if (startGameButton != null && !isTransitioning)
        {
            startGameButton.interactable = count >= minPlayersToStart;
        }

        UpdateStartButtonText();
    }

    void UpdateStartButtonText()
    {
        if (startButtonText == null) return;

        if (currentPlayerCount < minPlayersToStart)
        {
            startButtonText.text = $"EN ATTENTE ({currentPlayerCount}/{minPlayersToStart})";
        }
        else
        {
            startButtonText.text = "COMMENCER!";
        }
    }

    void OnStartButtonClicked()
    {
        if (isTransitioning) return;
        if (currentPlayerCount < minPlayersToStart) return;

        StartCoroutine(StartGameSequence());
    }

    // Peut etre appelee par un raccourci clavier aussi
    public void ForceStart()
    {
        if (!isTransitioning)
        {
            StartCoroutine(StartGameSequence());
        }
    }

    IEnumerator StartGameSequence()
    {
        isTransitioning = true;

        // Arreter le polling
        if (playerPollCoroutine != null)
        {
            StopCoroutine(playerPollCoroutine);
        }

        // Desactiver le bouton
        if (startGameButton != null)
        {
            startGameButton.interactable = false;
        }

        // ===== PHASE 4: INSTRUCTIONS (rapide) =====
        currentPhase = IntroPhase.Instructions;
        
        // Fade out QR, fade in instructions
        StartCoroutine(FadeCanvasGroup(qrCodePhaseGroup, 1, 0, 0.3f));
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(FadeCanvasGroup(instructionsPhaseGroup, 0, 1, 0.3f));

        // Afficher les instructions pendant 3 secondes
        yield return new WaitForSeconds(3f);

        // ===== PHASE 5: COMPTE A REBOURS =====
        currentPhase = IntroPhase.Countdown;

        // Fade out instructions, fade in countdown
        StartCoroutine(FadeCanvasGroup(instructionsPhaseGroup, 1, 0, 0.3f));
        yield return new WaitForSeconds(0.2f);
        yield return StartCoroutine(FadeCanvasGroup(countdownPhaseGroup, 0, 1, 0.3f));

        // Arreter la musique de titre
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        // Compte a rebours 3-2-1
        for (int i = 3; i > 0; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = i.ToString();

                // Animation de scale
                StartCoroutine(PulseText(countdownText.rectTransform));
            }

            if (audioSource != null && countdownBeep != null)
            {
                audioSource.PlayOneShot(countdownBeep);
            }

            yield return new WaitForSeconds(1f);
        }

        // SUMO!
        if (countdownText != null)
        {
            countdownText.text = "SUMO!";
            countdownText.color = new Color(1f, 0.3f, 0.1f); // Orange/rouge
            StartCoroutine(PulseText(countdownText.rectTransform, 1.5f));
        }

        if (countdownSubText != null)
        {
            countdownSubText.text = "";
        }

        if (audioSource != null && goSound != null)
        {
            audioSource.PlayOneShot(goSound);
        }

        yield return new WaitForSeconds(0.8f);

        // ===== PHASE 6: TRANSITION =====
        currentPhase = IntroPhase.Transition;
        yield return StartCoroutine(TransitionToGame());
    }

    IEnumerator PulseText(RectTransform textRect, float maxScale = 1.3f)
    {
        if (textRect == null) yield break;

        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = Vector3.one * maxScale;

        // Scale up
        float duration = 0.15f;
        float elapsed = 0;
        while (elapsed < duration)
        {
            textRect.localScale = Vector3.Lerp(originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Scale down
        elapsed = 0;
        while (elapsed < duration)
        {
            textRect.localScale = Vector3.Lerp(targetScale, originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        textRect.localScale = originalScale;
    }

    IEnumerator TransitionToGame()
    {
        // Notifier le serveur de demarrer la manche
        yield return StartCoroutine(NotifyServerStart());

        // Fade out final
        yield return StartCoroutine(FadeCanvasGroup(countdownPhaseGroup, 1, 0, 0.3f));

        // Charger la scene de jeu
        if (!string.IsNullOrEmpty(gameSceneName))
        {
            SceneManager.LoadScene(gameSceneName);
        }
        else
        {
            // Si meme scene, juste desactiver le Canvas d'intro
            gameObject.SetActive(false);

            // Informer le GameManager
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPhase(GameManager.GamePhase.Playing);
            }
        }
    }

    IEnumerator NotifyServerStart()
    {
        string url = $"http://localhost:{serverPort}/api/startRound";

        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Serveur notifie - Manche demarree!");
            }
            else
            {
                Debug.LogWarning("Erreur notification serveur: " + www.error);
            }
        }
    }

    // ==================== UTILITAIRES ====================

    void SetCanvasGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;

        group.alpha = visible ? 1 : 0;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;

        group.interactable = to > 0.5f;
        group.blocksRaycasts = to > 0.5f;

        float elapsed = 0;
        while (elapsed < duration)
        {
            group.alpha = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        group.alpha = to;
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
        catch (System.Exception)
        {
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

    void Update()
    {
        // Raccourci clavier pour forcer le demarrage (debug)
        if (Input.GetKeyDown(KeyCode.Space) && currentPhase == IntroPhase.QRCode)
        {
            ForceStart();
        }

        // Raccourci pour skip l'intro (debug)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllCoroutines();
            if (!string.IsNullOrEmpty(gameSceneName))
            {
                SceneManager.LoadScene(gameSceneName);
            }
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
