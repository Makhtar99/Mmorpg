using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkPlayer : MonoBehaviour
{
    public int Id;
    public bool IsLocal;

    public float MoveSpeed = 4.2f;
    public float RotateSpeed = 300f;
    public float SendsPerSecond = 15f;
    public float InterpolationSpeed = 12f;

    private GameClient _client;
    private MetaverseInput _input;
    private InputAction _move;
    private Animator _anim;
    private Rigidbody _rb;

    private Vector3 _targetPos;
    private float _targetYaw;
    private float _sendTimer;

    public void InitLocal(int id, GameClient client)
    {
        Id = id;
        IsLocal = true;
        _client = client;
        _anim = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody>();

        _input = new MetaverseInput();
        _move = _input.Player1.Move;
        _move.Enable();
    }

    public void InitRemote(int id, Vector3 pos, float yaw)
    {
        Id = id;
        IsLocal = false;
        _anim = GetComponentInChildren<Animator>();
        _rb = GetComponent<Rigidbody>();
        if (_rb != null) _rb.isKinematic = true;

        transform.position = pos;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        _targetPos = pos;
        _targetYaw = yaw;
    }

    public void SetNetworkTarget(Vector3 pos, float yaw)
    {
        _targetPos = pos;
        _targetYaw = yaw;
    }

    void Update()
    {
        if (!IsLocal)
        {
            UpdateRemote();
            return;
        }

        if (_rb == null) MoveByTransform();

        _sendTimer += Time.deltaTime;
        if (_sendTimer >= 1f / SendsPerSecond)
        {
            _sendTimer = 0f;
            _client.SendMove(transform.position, transform.eulerAngles.y);
        }
    }

    void FixedUpdate()
    {
        if (!IsLocal || _rb == null) return;

        Vector2 v = _move.ReadValue<Vector2>();
        if (_anim != null) _anim.SetFloat("Walk", v.y);

        _rb.angularVelocity = Vector3.zero;
        _rb.MovePosition(_rb.position + transform.forward * (MoveSpeed * Time.fixedDeltaTime * v.y));
        _rb.MoveRotation(_rb.rotation * Quaternion.AngleAxis(RotateSpeed * Time.fixedDeltaTime * v.x, Vector3.up));
    }

    private void MoveByTransform()
    {
        Vector2 v = _move.ReadValue<Vector2>();
        transform.Rotate(Vector3.up, v.x * RotateSpeed * Time.deltaTime);
        transform.position += transform.forward * (v.y * MoveSpeed * Time.deltaTime);
        if (_anim != null) _anim.SetFloat("Walk", v.y);
    }

    private void UpdateRemote()
    {
        Vector3 before = transform.position;
        transform.position = Vector3.Lerp(transform.position, _targetPos, Time.deltaTime * InterpolationSpeed);
        Quaternion target = Quaternion.Euler(0f, _targetYaw, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * InterpolationSpeed);

        if (_anim != null)
        {
            float moving = (transform.position - before).sqrMagnitude > 0.000001f ? 1f : 0f;
            _anim.SetFloat("Walk", moving);
        }
    }

    void OnDisable()
    {
        if (_move != null) _move.Disable();
    }
}
