using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Gestionnaire principal du jeu Sumo Panic Arena.
/// Gere les phases de jeu, le timer, le compte a rebours, etc.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration Serveur")]
    public string serverBaseUrl = "http://localhost:3000";
    
    [Header("Configuration de la manche")]
    [Tooltip("Duree maximale d'une manche en secondes")]
    public float roundDuration = 120f;
    
    [Tooltip("Duree du compte a rebours avant le debut")]
    public int countdownDuration = 3;

    [Header("References UI")]
    public TMP_Text timerText;
    public TMP_Text countdownText;
    public TMP_Text playerCountText;
    public TMP_Text statusText;
    public GameObject lobbyPanel;
    public GameObject gamePanel;
    public GameObject endRoundPanel;
    public TMP_Text winnerText;

    [Header("References Scripts")]
    public ArenaManager arenaManager;
    public RemotePlayersManager playersManager;

    [Header("Audio (Optionnel)")]
    public AudioSource musicSource;
    public AudioClip lobbyMusic;
    public AudioClip gameMusic;
    public AudioClip countdownSound;
    public AudioClip winSound;

    // Etat du jeu
    public enum GamePhase { Lobby, Countdown, Playing, RoundEnd }
    public GamePhase CurrentPhase { get; private set; } = GamePhase.Lobby;

    private float roundTimer;
    private int connectedPlayers = 0;
    private Coroutine playerCountCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Demarrer en mode Lobby
        SetPhase(GamePhase.Lobby);
        
        // Commencer a poll le nombre de joueurs
        playerCountCoroutine = StartCoroutine(PollPlayerCountLoop());
    }

    void Update()
    {
        if (CurrentPhase == GamePhase.Playing)
        {
            // Mettre a jour le timer
            roundTimer -= Time.deltaTime;
            UpdateTimerUI();

            if (roundTimer <= 0)
            {
                // Temps ecoule - fin de manche
                EndRound("Temps ecoule!");
            }
        }
    }

    /// <summary>
    /// Change la phase de jeu actuelle
    /// </summary>
    public void SetPhase(GamePhase newPhase)
    {
        CurrentPhase = newPhase;

        // Gerer l'UI selon la phase
        if (lobbyPanel != null) lobbyPanel.SetActive(newPhase == GamePhase.Lobby);
        if (gamePanel != null) gamePanel.SetActive(newPhase == GamePhase.Playing || newPhase == GamePhase.Countdown);
        if (endRoundPanel != null) endRoundPanel.SetActive(newPhase == GamePhase.RoundEnd);
        if (countdownText != null) countdownText.gameObject.SetActive(newPhase == GamePhase.Countdown);

        // Actions specifiques par phase
        switch (newPhase)
        {
            case GamePhase.Lobby:
                if (musicSource != null && lobbyMusic != null)
                {
                    musicSource.clip = lobbyMusic;
                    musicSource.Play();
                }
                break;

            case GamePhase.Playing:
                if (musicSource != null && gameMusic != null)
                {
                    musicSource.clip = gameMusic;
                    musicSource.Play();
                }
                break;
        }
    }

    /// <summary>
    /// Demarre le compte a rebours puis la manche
    /// </summary>
    public void StartCountdown()
    {
        if (CurrentPhase != GamePhase.Lobby) return;
        
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        SetPhase(GamePhase.Countdown);

        // Notifier le serveur que la manche commence
        yield return StartCoroutine(NotifyServerStartRound());

        // Compte a rebours
        for (int i = countdownDuration; i > 0; i--)
        {
            if (countdownText != null)
            {
                countdownText.text = i.ToString();
            }

            if (countdownSound != null && musicSource != null)
            {
                musicSource.PlayOneShot(countdownSound);
            }

            yield return new WaitForSeconds(1f);
        }

        // GO!
        if (countdownText != null)
        {
            countdownText.text = "SUMO!";
        }

        yield return new WaitForSeconds(0.5f);

        // Demarrer la manche
        StartRound();
    }

    IEnumerator NotifyServerStartRound()
    {
        string url = serverBaseUrl + "/api/startRound";
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Erreur startRound: " + www.error);
            }
        }
    }

    void StartRound()
    {
        SetPhase(GamePhase.Playing);
        roundTimer = roundDuration;

        // Demarrer l'effondrement de l'arene
        if (arenaManager != null)
        {
            arenaManager.StartAutoCollapse();
        }
    }

    /// <summary>
    /// Termine la manche avec un message
    /// </summary>
    public void EndRound(string message)
    {
        if (CurrentPhase != GamePhase.Playing) return;

        SetPhase(GamePhase.RoundEnd);

        if (arenaManager != null)
        {
            arenaManager.StopAutoCollapse();
        }

        if (winnerText != null)
        {
            winnerText.text = message;
        }

        if (winSound != null && musicSource != null)
        {
            musicSource.PlayOneShot(winSound);
        }
    }

    /// <summary>
    /// Annonce un gagnant
    /// </summary>
    public void AnnounceWinner(int playerId)
    {
        EndRound($"Joueur #{playerId} GAGNE!");
    }

    /// <summary>
    /// Recharge la scene pour une nouvelle manche
    /// </summary>
    public void RestartRound()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Retourne au lobby (recharge aussi la scene)
    /// </summary>
    public void ReturnToLobby()
    {
        // Notifier le serveur de reset
        StartCoroutine(ResetServerAndReload());
    }

    IEnumerator ResetServerAndReload()
    {
        string url = serverBaseUrl + "/api/resetRound";
        using (UnityWebRequest www = UnityWebRequest.PostWwwForm(url, ""))
        {
            yield return www.SendWebRequest();
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(roundTimer / 60);
            int seconds = Mathf.FloorToInt(roundTimer % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    IEnumerator PollPlayerCountLoop()
    {
        while (true)
        {
            string url = serverBaseUrl + "/api/playerCount";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    if (int.TryParse(www.downloadHandler.text, out int count))
                    {
                        connectedPlayers = count;
                        UpdatePlayerCountUI();
                    }
                }
            }

            yield return new WaitForSeconds(1f);
        }
    }

    void UpdatePlayerCountUI()
    {
        if (playerCountText != null)
        {
            playerCountText.text = $"Joueurs: {connectedPlayers}";
        }
    }

    /// <summary>
    /// Retourne le nombre de joueurs connectes
    /// </summary>
    public int GetConnectedPlayersCount()
    {
        return connectedPlayers;
    }
}
