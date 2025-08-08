using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToGas : MonoBehaviour
{
    [Header("씬에 미리 배치한 기체 입자들(부모 필요 없음)")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("원 중심(비우면 초기 무게중심 사용)")]
    public Transform designCenter;

    [Header("변환 설정")]
    public float initialForce = 2f;  // 0이면 퍼지는 힘 없음

    [Header("이동/점프")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    Rigidbody2D rb;
    Collider2D col;
    bool isGrounded;

    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CapturePattern(); // 씬에서 만든 모양 저장
    }

    void Start()
    {
        foreach (var g in gasParticles) if (g) g.SetActive(false); // 시작 시 숨김
    }

    void Update()
    {
        float move = Input.GetAxisRaw("Horizontal");
        if (move != 0f) rb.velocity = new Vector2(move * moveSpeed, rb.velocity.y);

        if (groundCheck)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);

        if (Input.GetKeyDown(KeyCode.X))
            TransformToGas();
    }

    void CapturePattern()
    {
        offsets.Clear();
        if (gasParticles.Count == 0) return;

        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in gasParticles) { if (!g) continue; sum += (Vector2)g.transform.position; n++; }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }

        foreach (var g in gasParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    void TransformToGas()
    {
        Vector2 spawnCenter = col ? (Vector2)col.bounds.center : (Vector2)transform.position;

        for (int i = 0; i < gasParticles.Count; i++)
        {
            var g = gasParticles[i];
            if (!g) continue;

            // 조상 비활성이면 이 입자만 월드로 분리(형제들은 그대로 숨김)
            if (HasInactiveAncestor(g.transform))
                g.transform.SetParent(null, true);

            if (!g.activeSelf) g.SetActive(true);

            Vector2 off = i < offsets.Count ? offsets[i] : Vector2.zero;
            g.transform.position = spawnCenter + off;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Vector2.zero;
                grb.velocity = dir * initialForce;
            }
        }

        Destroy(gameObject); // 변환 후 고체 제거
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
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}

