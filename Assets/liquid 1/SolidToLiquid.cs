// SolidToLiquid2D.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquid2D : MonoBehaviour
{
    [Header("���� �̸� ��ġ�� ��ü ���ڵ�(������ ������ ���� ��� ���)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("������ ������(���� ���� �����ġ/�ڱ� ��ġ)")]
    public Transform designCenter;

    [Header("������ ���� ���(�̹�ġ �� ���)")]
    public GameObject liquidParticlePrefab;
    [Min(1)] public int particleCount = 50;
    public float spawnRadius = 0.35f;

    [Header("��ȯ �� �ʱ� ��")]
    public float initialForce = 2f;       // 0�̸� ������ �� ����
    public bool inheritVelocity = true;   // ���� �ӵ� ���

    [Header("�̵�/����")]
    public float moveForce = 20f;         // �¿� ���� ��
    public float maxSpeed = 6f;           // ���� �ִ� �ӵ�
    public float jumpVelocity = 12f;      // ���� ��� �ӵ�(��� ����)
    public Transform groundCheck;         // �� �Ʒ� �� ������Ʈ
    public float groundCheckRadius = 0.20f;
    public LayerMask groundLayer;

    [Header("���� ����(�������� �� ���� ���� ����)")]
    [SerializeField] float coyoteTime = 0.1f;   // ������ ���� ��� �ð�
    [SerializeField] float jumpBuffer = 0.12f;  // �̸� ���� ���� �Է� ����

    [Header("Ű ����")]
    public KeyCode morphKey = KeyCode.M;

    Rigidbody2D rb;
    Collider2D col;

    // �̵�/���� ����
    float moveInput;
    bool isGrounded;
    float lastGroundTime = -999f;
    float lastJumpPressTime = -999f;

    // �̸� ��ġ�� ���ڵ��� ��� ������ ����
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CapturePattern();  // �� ��ġ ���� ����

        // groundCheck �ڵ� ����(���� ��)
        if (!groundCheck)
        {
            var gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            groundCheck = gc.transform;
        }
    }

    void Start()
    {
        // �̸� ��ġ�� ���ڵ��� ���� �� ����
        foreach (var g in preplacedParticles)
            if (g) g.SetActive(false);
    }

    void Update()
    {
        // �¿� �Է�(ȭ��ǥ ����)
        moveInput =
            (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) +
            (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f);

        // ���� �Է��� ���ۿ� ���
        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressTime = Time.time;

        // ���� ��ȯ
        if (Input.GetKeyDown(morphKey))
            TransformToLiquid();
    }

    void FixedUpdate()
    {
        // ���� ����(���� ������ + ���� ���� �˻�)
        isGrounded = IsGrounded();
        if (isGrounded) lastGroundTime = Time.time;

        // �� ��� �̵�(���鿡�� �ڿ�������)
        if (Mathf.Abs(rb.velocity.x) < maxSpeed || Mathf.Sign(moveInput) != Mathf.Sign(rb.velocity.x))
        {
            rb.AddForce(new Vector2(moveInput * moveForce, 0f), ForceMode2D.Force);
        }

        // ����: ���� + �ڿ���Ÿ��
        bool canCoyote = Time.time - lastGroundTime <= coyoteTime;
        bool hasBuffered = Time.time - lastJumpPressTime <= jumpBuffer;

        if (hasBuffered && canCoyote)
        {
            var v = rb.velocity;
            v.y = jumpVelocity;   // Ȯ���� ��� �ӵ� �ο�
            rb.velocity = v;

            lastJumpPressTime = -999f; // �Է� �Һ�
        }
    }

    bool IsGrounded()
    {
        // 1) groundCheck ���� ������
        bool circ = groundCheck && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (circ) return true;

        // 2) ���� ���� �˻�(������ ������)
        //   - ������ y ������ ����� ��(0.5 �̻�)�̸� �ٴ����� ����
        ContactPoint2D[] contacts = new ContactPoint2D[8];
        int count = rb.GetContacts(contacts);
        for (int i = 0; i < count; i++)
        {
            var other = contacts[i].collider;
            if (((1 << other.gameObject.layer) & groundLayer) == 0) continue;
            if (contacts[i].normal.y > 0.5f) return true;
        }
        return false;
    }

    void CapturePattern()
    {
        offsets.Clear();
        if (preplacedParticles.Count == 0) return;

        // ������ ���
        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in preplacedParticles)
            {
                if (!g) continue;
                sum += (Vector2)g.transform.position; n++;
            }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }

        // ��� ������ ���
        foreach (var g in preplacedParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    void TransformToLiquid()
    {
        Vector2 center = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 inheritVel = inheritVelocity ? rb.velocity : Vector2.zero;

        // ��ȯ �� �ڱ� �浹/�ð� ��Ȱ��ȭ
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        rb.simulated = false;

        if (preplacedParticles.Count > 0)
        {
            // ��� 1: �̸� ��ġ�� ���� ���ġ/Ȱ��
            for (int i = 0; i < preplacedParticles.Count; i++)
            {
                var g = preplacedParticles[i];
                if (!g) continue;

                // ��Ȱ�� �θ� �Ʒ� ������ ����� �и�
                if (HasInactiveAncestor(g.transform))
                    g.transform.SetParent(null, true);

                if (!g.activeSelf) g.SetActive(true);

                Vector2 off = i < offsets.Count ? offsets[i] : Vector2.zero;
                g.transform.position = center + off;

                var grb = g.GetComponent<Rigidbody2D>();
                if (grb)
                {
                    Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Random.insideUnitCircle.normalized;
                    grb.velocity = inheritVel + dir * initialForce;
                }
            }
        }
        else
        {
            // ��� 2: ������ ����
            if (!liquidParticlePrefab)
            {
                Debug.LogWarning("liquidParticlePrefab�� ����ְ�, preplacedParticles�� �����ϴ�.");
            }
            else
            {
                for (int i = 0; i < particleCount; i++)
                {
                    Vector2 off = Random.insideUnitCircle * spawnRadius;
                    var rot = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                    var p = Instantiate(liquidParticlePrefab, center + off, rot);

                    var prb = p.GetComponent<Rigidbody2D>();
                    if (prb)
                    {
                        Vector2 dir = (off.sqrMagnitude > 1e-8f) ? off.normalized : Random.insideUnitCircle.normalized;
                        prb.velocity = inheritVel + dir * initialForce;
                    }
                }
            }
        }

        // ��ü ����
        Destroy(gameObject);
    }

    static bool HasInactiveAncestor(Transform t)
    {
        for (Transform a = t.parent; a != null; a = a.parent)
            if (!a.gameObject.activeSelf) return true;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        if (preplacedParticles != null && preplacedParticles.Count > 0 && designCenter)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(designCenter.position, 0.04f);
        }
    }
}
