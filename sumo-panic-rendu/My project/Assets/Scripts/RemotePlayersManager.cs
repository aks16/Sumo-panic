using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

[System.Serializable]
public class PlayerDto
{
    public int id;
    public float h;
    public float v;
    public bool push;
}

[System.Serializable]
public class PlayersResponse
{
    public PlayerDto[] players;
}

[System.Serializable]
public class TrapDto
{
    public int id;
    public int playerId;
    public string type;
}

[System.Serializable]
public class TrapsResponse
{
    public TrapDto[] traps;
}

public class RemotePlayersManager : MonoBehaviour
{
    [Header("Réseau")]
    public string playersUrl = "http://localhost:3000/api/players";
    public string serverBaseUrl = "http://localhost:3000";
    public string trapsUrl = "http://localhost:3000/api/traps";
    public GameObject sumoPrefab;

    [Header("Spawn")]
    public float spawnRadius = 4f;
    public float spawnY = 1.5f;

    [Header("Règles de jeu")]
    public float fallY = -5f;

    [Header("UI")]
    public TMP_Text centerStatusText;
    public TMP_Text joinText;

    [Header("Événements spéciaux")]
    public GameObject rhinoPrefab;
    public bool rhinoEventsEnabled = true;
    public float rhinoMinInterval = 10f;
    public float rhinoMaxInterval = 20f;
    public float rhinoSpawnDistance = 15f; // distance du centre où spawn le rhino
    public float rhinoY = 1.5f;

    private class LocalPlayer
    {
        public PlayerDto dto;
        public GameObject go;
        public SumoController controller;
        public bool seenThisFrame;
        public bool isAlive = true;
    }

    private Dictionary<int, LocalPlayer> locals = new Dictionary<int, LocalPlayer>();

    private Coroutine centerRoutine;
    private Coroutine joinRoutine;

    void Start()
    {
        StartCoroutine(PollPlayersLoop());
        StartCoroutine(PollTrapsLoop());

        if (rhinoEventsEnabled && rhinoPrefab != null)
            StartCoroutine(RhinoEventLoop());

        ShowCenterStatus("Prêt à jouer", 0f);

        if (joinText != null)
            joinText.text = "";
    }

    void Update()
    {
        // Détection des chutes côté Unity
        foreach (var kvp in locals)
        {
            int playerId = kvp.Key;
            LocalPlayer lp = kvp.Value;

            if (lp.isAlive && lp.go != null && lp.go.transform.position.y < fallY)
            {
                EliminatePlayer(playerId);
            }
        }
    }

    IEnumerator PollPlayersLoop()
    {
        while (true)
        {
            using (var www = UnityWebRequest.Get(playersUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string json = www.downloadHandler.text;
                    PlayersResponse resp = JsonUtility.FromJson<PlayersResponse>(json);

                    foreach (var lp in locals.Values)
                        lp.seenThisFrame = false;

                    if (resp != null && resp.players != null)
                    {
                        foreach (var p in resp.players)
                        {
                            LocalPlayer lp;
                            if (!locals.TryGetValue(p.id, out lp))
                            {
                                // nouveau joueur -> spawn
                                Vector2 pos2D = Random.insideUnitCircle * spawnRadius;
                                Vector3 pos = new Vector3(pos2D.x, spawnY, pos2D.y);

                                GameObject go = Instantiate(sumoPrefab, pos, Quaternion.identity);
                                SumoController ctrl = go.GetComponent<SumoController>();

                                lp = new LocalPlayer
                                {
                                    dto = p,
                                    go = go,
                                    controller = ctrl,
                                    seenThisFrame = true,
                                    isAlive = true
                                };
                                locals.Add(p.id, lp);

                                ShowJoinMessage($"Joueur #{p.id} rejoint la partie", 2f);
                            }
                            else
                            {
                                lp.seenThisFrame = true;
                            }

                            // inputs pour les vivants
                            lp.dto = p;
                            if (lp.isAlive && lp.controller != null)
                            {
                                lp.controller.SetInput(p.h, p.v, p.push);
                            }
                        }
                    }

                    // supprimer les joueurs non vus
                    List<int> toRemove = new List<int>();
                    foreach (var kvp in locals)
                    {
                        if (!kvp.Value.seenThisFrame)
                        {
                            if (kvp.Value.go != null)
                                Destroy(kvp.Value.go);
                            toRemove.Add(kvp.Key);
                        }
                    }
                    foreach (var id in toRemove)
                        locals.Remove(id);
                }
                else
                {
                    Debug.LogWarning("Erreur /api/players : " + www.error);
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    IEnumerator PollTrapsLoop()
    {
        while (true)
        {
            using (var www = UnityWebRequest.Get(trapsUrl))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string json = www.downloadHandler.text;
                    TrapsResponse resp = JsonUtility.FromJson<TrapsResponse>(json);
                    if (resp != null && resp.traps != null)
                    {
                        foreach (var t in resp.traps)
                        {
                            ApplyTrap(t);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Erreur /api/traps : " + www.error);
                }
            }

            yield return new WaitForSeconds(0.1f);
        }
    }

    void EliminatePlayer(int playerId)
    {
        LocalPlayer lp;
        if (!locals.TryGetValue(playerId, out lp))
            return;

        if (!lp.isAlive)
            return;

        lp.isAlive = false;

        if (lp.go != null)
        {
            Destroy(lp.go);
            lp.go = null;
        }

        ShowCenterStatus($"Joueur #{playerId} est éliminé !", 2f);
        StartCoroutine(NotifyDeathToServer(playerId));
        CheckForWinner();
    }

    IEnumerator NotifyDeathToServer(int playerId)
    {
        string url = serverBaseUrl + "/api/playerEliminated?id=" + playerId;
        using (var www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Erreur /api/playerEliminated : " + www.error);
            }
        }
    }

    void CheckForWinner()
    {
        int aliveCount = 0;
        int lastAliveId = -1;

        foreach (var kvp in locals)
        {
            if (kvp.Value.isAlive)
            {
                aliveCount++;
                lastAliveId = kvp.Key;
            }
        }

        if (aliveCount == 0)
        {
            ShowCenterStatus("Tous les joueurs sont éliminés !", 0f);
        }
        else if (aliveCount == 1)
        {
            ShowCenterStatus($"Joueur #{lastAliveId} a gagné la manche !", 0f);
        }
    }

    // --------- Application des pièges fantômes ---------

    void ApplyTrap(TrapDto t)
    {
        List<LocalPlayer> alive = new List<LocalPlayer>();
        foreach (var kvp in locals)
        {
            var lp = kvp.Value;
            if (lp.isAlive && lp.go != null && lp.controller != null)
            {
                alive.Add(lp);
            }
        }

        if (alive.Count == 0) return;

        LocalPlayer target = alive[Random.Range(0, alive.Count)];
        Rigidbody rb = target.go.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 arenaCenter = Vector3.zero;

        if (t.type == "explosion")
        {
            Vector3 toPlayer = (target.go.transform.position - arenaCenter);
            toPlayer.y = 0f;
            Vector3 horizontalDir = toPlayer.sqrMagnitude > 0.01f
                ? toPlayer.normalized
                : new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;

            float horizontalForce = 10f;
            float verticalForce = 6f;

            Vector3 force = horizontalDir * horizontalForce + Vector3.up * verticalForce;
            rb.AddForce(force, ForceMode.Impulse);

            rb.AddTorque(new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f),
                Random.Range(-5f, 5f)
            ), ForceMode.Impulse);

            ShowCenterStatus($"Un fantôme explose le joueur #{target.dto.id} !", 2f);
        }
        else if (t.type == "banana")
        {
            Vector3 slideDir = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;

            float slideForce = 8f;
            Vector3 force = slideDir * slideForce;

            rb.AddForce(force, ForceMode.Impulse);

            ShowCenterStatus($"Un fantôme fait déraper le joueur #{target.dto.id} !", 2f);
        }
    }

    // --------- Événement Rhino ---------

    IEnumerator RhinoEventLoop()
    {
        while (true)
        {
            float wait = Random.Range(rhinoMinInterval, rhinoMaxInterval);
            yield return new WaitForSeconds(wait);

            SpawnRhinoEvent();
        }
    }

    void SpawnRhinoEvent()
    {
        if (rhinoPrefab == null) return;

        // Choisir un côté : gauche/droite ou avant/arrière
        Vector3 dir;
        switch (Random.Range(0, 4))
        {
            case 0: dir = Vector3.right; break;   // gauche -> droite
            case 1: dir = Vector3.left; break;   // droite -> gauche
            case 2: dir = Vector3.forward; break; // arrière -> avant
            default: dir = Vector3.back; break;   // avant -> arrière
        }

        // On spawn le rhino en dehors de l'arène, pointant vers l'intérieur
        Vector3 spawnPos = -dir * rhinoSpawnDistance;
        spawnPos.y = rhinoY;

        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        Instantiate(rhinoPrefab, spawnPos, rot);

        ShowCenterStatus("Un rhinocéros déchaîné traverse l'arène !", 2f);
    }

    // --------- UI : messages centraux ---------

    void ShowCenterStatus(string msg, float duration = 0f)
    {
        Debug.Log(msg);

        if (centerRoutine != null)
            StopCoroutine(centerRoutine);

        centerRoutine = StartCoroutine(CenterStatusRoutine(msg, duration));
    }

    IEnumerator CenterStatusRoutine(string msg, float duration)
    {
        if (centerStatusText != null)
            centerStatusText.text = msg;

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            if (centerStatusText != null)
                centerStatusText.text = "";
        }
    }

    // --------- UI : messages de join ---------

    void ShowJoinMessage(string msg, float duration = 2f)
    {
        Debug.Log(msg);

        if (joinRoutine != null)
            StopCoroutine(joinRoutine);

        joinRoutine = StartCoroutine(JoinMessageRoutine(msg, duration));
    }

    IEnumerator JoinMessageRoutine(string msg, float duration)
    {
        if (joinText != null)
            joinText.text = msg;

        if (duration > 0f)
        {
            yield return new WaitForSeconds(duration);
            if (joinText != null)
                joinText.text = "";
        }
    }
}