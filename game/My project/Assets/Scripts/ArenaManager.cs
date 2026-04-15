using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gere l'effondrement progressif de l'arene pendant la partie.
/// Les tuiles les plus eloignees du centre tombent en premier.
/// </summary>
public class ArenaManager : MonoBehaviour
{
    [Header("Effondrement automatique")]
    [Tooltip("Activer l'effondrement automatique des tuiles")]
    public bool autoCollapseEnabled = true;
    
    [Tooltip("Delai avant le premier effondrement (secondes)")]
    public float initialDelay = 10f;
    
    [Tooltip("Intervalle entre chaque effondrement (secondes)")]
    public float collapseInterval = 3f;
    
    [Tooltip("Nombre de tuiles qui tombent a chaque vague")]
    public int tilesPerWave = 2;
    
    [Tooltip("Reduire l'intervalle progressivement")]
    public bool accelerateOverTime = true;
    
    [Tooltip("Intervalle minimum (si acceleration activee)")]
    public float minInterval = 1f;
    
    [Tooltip("Reduction de l'intervalle par vague")]
    public float intervalReduction = 0.2f;

    [Header("References")]
    [Tooltip("Point central de l'arene (laisser vide = Vector3.zero)")]
    public Transform arenaCenter;

    // Liste des tuiles triees par distance au centre
    private List<ArenaSection> arenaTiles = new List<ArenaSection>();
    private bool isCollapsing = false;
    private float currentInterval;

    void Start()
    {
        currentInterval = collapseInterval;
        RefreshTilesList();
    }

    /// <summary>
    /// Recupere toutes les tuiles de l'arene et les trie par distance au centre
    /// </summary>
    public void RefreshTilesList()
    {
        arenaTiles.Clear();
        
        ArenaSection[] allTiles = FindObjectsByType<ArenaSection>(FindObjectsSortMode.None);
        
        Vector3 center = arenaCenter != null ? arenaCenter.position : Vector3.zero;
        
        // Trier par distance au centre (les plus eloignees en premier)
        System.Array.Sort(allTiles, (a, b) => 
        {
            float distA = Vector3.Distance(a.transform.position, center);
            float distB = Vector3.Distance(b.transform.position, center);
            return distB.CompareTo(distA); // Ordre decroissant
        });
        
        arenaTiles.AddRange(allTiles);
    }

    /// <summary>
    /// Demarre l'effondrement automatique (appele quand la manche commence)
    /// </summary>
    public void StartAutoCollapse()
    {
        if (!autoCollapseEnabled) return;
        if (isCollapsing) return;
        
        RefreshTilesList();
        StartCoroutine(AutoCollapseRoutine());
    }

    /// <summary>
    /// Arrete l'effondrement (appele quand la manche se termine)
    /// </summary>
    public void StopAutoCollapse()
    {
        isCollapsing = false;
        StopAllCoroutines();
    }

    /// <summary>
    /// Remet l'arene a zero (pour une nouvelle manche)
    /// Note: Necessite de recharger la scene ou d'avoir un systeme de pooling
    /// </summary>
    public void ResetArena()
    {
        StopAutoCollapse();
        currentInterval = collapseInterval;
        // Les tuiles sont detruites, il faudra recharger la scene
    }

    IEnumerator AutoCollapseRoutine()
    {
        isCollapsing = true;
        
        // Attendre le delai initial
        yield return new WaitForSeconds(initialDelay);
        
        while (isCollapsing && arenaTiles.Count > 0)
        {
            // Faire tomber les tuiles de cette vague
            int tilesToCollapse = Mathf.Min(tilesPerWave, arenaTiles.Count);
            
            for (int i = 0; i < tilesToCollapse; i++)
            {
                if (arenaTiles.Count == 0) break;
                
                // Prendre la tuile la plus eloignee
                ArenaSection tile = arenaTiles[0];
                arenaTiles.RemoveAt(0);
                
                if (tile != null)
                {
                    tile.TriggerCollapse();
                }
            }
            
            // Accelerer si active
            if (accelerateOverTime)
            {
                currentInterval = Mathf.Max(minInterval, currentInterval - intervalReduction);
            }
            
            yield return new WaitForSeconds(currentInterval);
        }
        
        isCollapsing = false;
    }

    /// <summary>
    /// Fait tomber une tuile aleatoire (pour les tests)
    /// </summary>
    [ContextMenu("Test: Collapse Random Tile")]
    public void CollapseRandomTile()
    {
        if (arenaTiles.Count == 0)
        {
            RefreshTilesList();
        }
        
        if (arenaTiles.Count > 0)
        {
            int index = Random.Range(0, arenaTiles.Count);
            ArenaSection tile = arenaTiles[index];
            arenaTiles.RemoveAt(index);
            
            if (tile != null)
            {
                tile.TriggerCollapse();
            }
        }
    }

    /// <summary>
    /// Fait tomber les tuiles les plus proches d'une position
    /// (utile pour les pieges ou evenements)
    /// </summary>
    public void CollapseNearPosition(Vector3 position, int count = 1)
    {
        if (arenaTiles.Count == 0) return;
        
        // Trier temporairement par proximite a la position
        arenaTiles.Sort((a, b) => 
        {
            if (a == null) return 1;
            if (b == null) return -1;
            
            float distA = Vector3.Distance(a.transform.position, position);
            float distB = Vector3.Distance(b.transform.position, position);
            return distA.CompareTo(distB);
        });
        
        for (int i = 0; i < Mathf.Min(count, arenaTiles.Count); i++)
        {
            ArenaSection tile = arenaTiles[i];
            if (tile != null)
            {
                tile.TriggerCollapse();
            }
        }
        
        // Retirer les tuiles effondrees
        arenaTiles.RemoveAll(t => t == null);
    }

    /// <summary>
    /// Retourne le nombre de tuiles restantes
    /// </summary>
    public int GetRemainingTilesCount()
    {
        arenaTiles.RemoveAll(t => t == null);
        return arenaTiles.Count;
    }
}
