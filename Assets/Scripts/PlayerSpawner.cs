using UnityEngine;
using Unity.Cinemachine;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Prefabs — même ordre que les boutons")]
    public GameObject engineerPrefab;
    public GameObject knightPrefab;
    public GameObject roguePrefab;
    public GameObject magePrefab;
    public GameObject barbarianPrefab;
    public GameObject druidPrefab;

    public Transform spawnPoint;

    void Start()
    {
        string character = PlayerPrefs.GetString("SelectedCharacter", "engineer");

        GameObject prefab = character switch
        {
            "knight"    => knightPrefab,
            "rogue"     => roguePrefab,
            "mage"      => magePrefab,
            "barbarian" => barbarianPrefab,
            "druid"     => druidPrefab,
            _           => engineerPrefab  // défaut = engineer
        };

        if (prefab == null)
        {
            Debug.LogError($"Prefab manquant pour le personnage sélectionné: {character}");
            return;
        }

        Transform resolvedSpawnPoint = spawnPoint;
        if (resolvedSpawnPoint == null)
        {
            GameObject fallbackSpawnPoint = GameObject.Find("SpawnPoint");
            if (fallbackSpawnPoint != null)
            {
                resolvedSpawnPoint = fallbackSpawnPoint.transform;
            }
        }

        Vector3 spawnPosition = resolvedSpawnPoint != null ? resolvedSpawnPoint.position : transform.position;
        Quaternion spawnRotation = resolvedSpawnPoint != null ? resolvedSpawnPoint.rotation : transform.rotation;

        GameObject spawnedCharacter = Instantiate(prefab, spawnPosition, spawnRotation);

        CinemachineCamera cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = spawnedCharacter.transform;
            cinemachineCamera.LookAt = spawnedCharacter.transform;
        }

        Debug.Log($"Spawned {character} at {spawnPosition}");
    }
}