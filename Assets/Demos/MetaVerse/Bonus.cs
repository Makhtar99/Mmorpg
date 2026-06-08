using UnityEngine;

public class Bonus : MonoBehaviour
{
    public int BonusId;
    public LayerMask CollisionLayers;
    public int Points = 1;

    void Start()
    {
        if (GameClient.Instance != null)
            GameClient.Instance.RegisterBonus(this);
    }

    private bool ShouldHandleObject(Collider other) {
       return (CollisionLayers.value & (1 << other.gameObject.layer)) > 0;
    }

    void OnTriggerEnter(Collider other) {
      if (!ShouldHandleObject(other)) { return; }

      NetworkPlayer player = other.GetComponentInParent<NetworkPlayer>();
      if (player == null || !player.IsLocal) { return; }

      GameClient.Instance.SendPickupRequest(BonusId);
    }
}
