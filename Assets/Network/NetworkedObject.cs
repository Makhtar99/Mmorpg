using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class NetworkedObject : MonoBehaviour
{
    public int ObjectId;
    public float InterpolationSpeed = 10f;

    public static readonly List<NetworkedObject> All = new List<NetworkedObject>();
    private static readonly Dictionary<int, NetworkedObject> _byId = new Dictionary<int, NetworkedObject>();
    private static bool _idsAssigned;

    private bool _remote;
    private Vector3 _targetPos;
    private float _targetYaw;

    void Awake()
    {
        All.Add(this);
        _idsAssigned = false;
    }

    void OnDestroy()
    {
        All.Remove(this);
        _idsAssigned = false;
    }

    public static void EnsureIds()
    {
        if (_idsAssigned) return;
        _idsAssigned = true;

        _byId.Clear();
        List<NetworkedObject> sorted = new List<NetworkedObject>(All);
        sorted.Sort((a, b) => string.CompareOrdinal(a.HierarchyPath(), b.HierarchyPath()));

        for (int i = 0; i < sorted.Count; i++)
        {
            sorted[i].ObjectId = i + 1;
            _byId[i + 1] = sorted[i];
        }
    }

    public static NetworkedObject Find(int id)
    {
        EnsureIds();
        _byId.TryGetValue(id, out NetworkedObject o);
        return o;
    }

    private string HierarchyPath()
    {
        StringBuilder sb = new StringBuilder();
        Transform t = transform;
        while (t != null)
        {
            sb.Insert(0, "/" + t.name + "#" + t.GetSiblingIndex());
            t = t.parent;
        }
        return sb.ToString();
    }

    public void SetAsRemote()
    {
        _remote = true;
        _targetPos = transform.position;
        _targetYaw = transform.eulerAngles.y;
    }

    public void SetNetworkTarget(Vector3 pos, float yaw)
    {
        _targetPos = pos;
        _targetYaw = yaw;
    }

    void LateUpdate()
    {
        if (!_remote) return;
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * InterpolationSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _targetYaw, 0f), Time.deltaTime * InterpolationSpeed);
    }
}
