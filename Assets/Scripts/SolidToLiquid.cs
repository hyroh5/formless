// SolidToLiquid2D.cs ? Gas ���ϰ� ����, ��ü ����/���� ����
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquid2D : MonoBehaviour
{
    [Header("���� �̸� ��ġ�� ��ü ���ڵ�(�θ� �Ǵ� ����)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("������ ������(���� ���� �����ġ/�ڱ� ��ġ)")]
    public Transform designCenter;

    [Header("��ȯ �� �ʱ� ��")]
    public float initialForce = 2f;       // 0�̸� ������ �� ����
    public bool inheritVelocity = true;   // ��ü �ӵ� ���

    // ====== ��ü �̵�/���� (�� ���� ����) ======
    [Header("�̵�/����")]
    public float moveForce = 20f;
    public float maxSpeed = 6f;
    public float jumpVelocity = 12f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.20f;
    public LayerMask groundLayer;

    [Header("���� ����")]
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBuffer = 0.12f;

    [Header("Ű ����")]
    public KeyCode morphKey = KeyCode.M;

    Rigidbody2D rb;
    Collider2D col;

    float moveInput;
    bool isGrounded;
    float lastGroundTime = -999f;
    float lastJumpPressTime = -999f;

    // �̸� ��ġ�� ���ڵ��� ��� ������ ����
    readonly List<Vector2> offsets = new List<Vector2>();

    // ��ü ���� ĳ��(����/ǥ�� ��ȯ)
    SpriteRenderer[] cachedRenderers;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        CapturePattern();

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
        // ���� ����: ���� �� ��ü���� ����
        foreach (var g in preplacedParticles)
            if (g) g.SetActive(false);
    }

    void Update()
    {
        if (rb.simulated)
        {
            moveInput =
                (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) +
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f);

            if (Input.GetKeyDown(KeyCode.Space))
                lastJumpPressTime = Time.time;

            if (Input.GetKeyDown(morphKey))
                TransformToLiquid();
        }
        else
        {
            moveInput = 0f; // ��ü ���¿��� ����
        }
    }

    void FixedUpdate()
    {
        if (!rb.simulated) return;

        isGrounded = IsGrounded();
        if (isGrounded) lastGroundTime = Time.time;

        if (Mathf.Abs(rb.velocity.x) < maxSpeed || Mathf.Sign(moveInput) != Mathf.Sign(rb.velocity.x))
            rb.AddForce(new Vector2(moveInput * moveForce, 0f), ForceMode2D.Force);

        bool canCoyote = Time.time - lastGroundTime <= coyoteTime;
        bool hasBuffered = Time.time - lastJumpPressTime <= jumpBuffer;

        if (hasBuffered && canCoyote)
        {
            var v = rb.velocity;
            v.y = jumpVelocity;
            rb.velocity = v;
            lastJumpPressTime = -999f;
        }
    }

    bool IsGrounded()
    {
        bool circ = groundCheck && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (circ) return true;

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

        // ������
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

        foreach (var g in preplacedParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    static bool HasInactiveAncestor(Transform t)
    {
        for (Transform a = t.parent; a != null; a = a.parent)
            if (!a.gameObject.activeSelf) return true;
        return false;
    }

    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }

    void TransformToLiquid()
    {
        Vector2 center = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 inheritVel = inheritVelocity ? rb.velocity : Vector2.zero;

        // === ��ü ���� & ���� OFF (���� ��ȯ) ===
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        rb.simulated = false;

        if (cachedRenderers != null)
            foreach (var sr in cachedRenderers) if (sr) sr.enabled = false;

        // === �̸� ��ġ�� ��ü�� ��Ȱ�� (���� �ִ� �͸� ���) ===
        for (int i = 0; i < preplacedParticles.Count; i++)
        {
            var g = preplacedParticles[i];
            if (!g) continue;

            if (HasInactiveAncestor(g.transform))
                g.transform.SetParent(null, true);

            SetHierarchyActive(g, true);

            Vector2 off = (i < offsets.Count) ? offsets[i] : Vector2.zero;
            var pos = center + off;
            g.transform.position = new Vector3(pos.x, pos.y, 0f);

            var cols = g.GetComponentsInChildren<Collider2D>(true);
            foreach (var cc in cols) if (cc) cc.enabled = true;

            var srs = g.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs) if (sr) sr.enabled = true;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                grb.simulated = true;
                grb.WakeUp();
                Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Random.insideUnitCircle.normalized;
                grb.velocity = inheritVel + dir * initialForce;
            }
        }
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
