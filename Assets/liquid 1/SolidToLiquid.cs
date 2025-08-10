// SolidToLiquid2D.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquid2D : MonoBehaviour
{
    [Header("씬에 미리 배치한 액체 입자들(없으면 프리팹 생성 경로 사용)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("디자인 기준점(비우면 입자 평균위치/자기 위치)")]
    public Transform designCenter;

    [Header("프리팹 생성 경로(미배치 시 사용)")]
    public GameObject liquidParticlePrefab;
    [Min(1)] public int particleCount = 50;
    public float spawnRadius = 0.35f;

    [Header("변환 시 초기 힘")]
    public float initialForce = 2f;       // 0이면 퍼지는 힘 없음
    public bool inheritVelocity = true;   // 원의 속도 상속

    [Header("이동/점프")]
    public float moveForce = 20f;         // 좌우 가속 힘
    public float maxSpeed = 6f;           // 수평 최대 속도
    public float jumpVelocity = 12f;      // 점프 상승 속도(즉시 적용)
    public Transform groundCheck;         // 발 아래 빈 오브젝트
    public float groundCheckRadius = 0.20f;
    public LayerMask groundLayer;

    [Header("점프 보정(굴러가는 중 접지 끊김 대응)")]
    [SerializeField] float coyoteTime = 0.1f;   // 떨어진 직후 허용 시간
    [SerializeField] float jumpBuffer = 0.12f;  // 미리 누른 점프 입력 저장

    [Header("키 설정")]
    public KeyCode morphKey = KeyCode.M;

    Rigidbody2D rb;
    Collider2D col;

    // 이동/점프 상태
    float moveInput;
    bool isGrounded;
    float lastGroundTime = -999f;
    float lastJumpPressTime = -999f;

    // 미리 배치한 입자들의 상대 오프셋 저장
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CapturePattern();  // 씬 배치 패턴 저장

        // groundCheck 자동 생성(없을 때)
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
        // 미리 배치한 입자들은 시작 시 숨김
        foreach (var g in preplacedParticles)
            if (g) g.SetActive(false);
    }

    void Update()
    {
        // 좌우 입력(화살표 전용)
        moveInput =
            (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) +
            (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f);

        // 점프 입력을 버퍼에 기록
        if (Input.GetKeyDown(KeyCode.Space))
            lastJumpPressTime = Time.time;

        // 상태 변환
        if (Input.GetKeyDown(morphKey))
            TransformToLiquid();
    }

    void FixedUpdate()
    {
        // 접지 판정(원형 오버랩 + 접촉 법선 검사)
        isGrounded = IsGrounded();
        if (isGrounded) lastGroundTime = Time.time;

        // 힘 기반 이동(경사면에서 자연스럽게)
        if (Mathf.Abs(rb.velocity.x) < maxSpeed || Mathf.Sign(moveInput) != Mathf.Sign(rb.velocity.x))
        {
            rb.AddForce(new Vector2(moveInput * moveForce, 0f), ForceMode2D.Force);
        }

        // 점프: 버퍼 + 코요테타임
        bool canCoyote = Time.time - lastGroundTime <= coyoteTime;
        bool hasBuffered = Time.time - lastJumpPressTime <= jumpBuffer;

        if (hasBuffered && canCoyote)
        {
            var v = rb.velocity;
            v.y = jumpVelocity;   // 확실한 상승 속도 부여
            rb.velocity = v;

            lastJumpPressTime = -999f; // 입력 소비
        }
    }

    bool IsGrounded()
    {
        // 1) groundCheck 원형 오버랩
        bool circ = groundCheck && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (circ) return true;

        // 2) 접촉 법선 검사(굴러도 안정적)
        //   - 법선의 y 성분이 충분히 위(0.5 이상)이면 바닥으로 간주
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

        // 기준점 계산
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

        // 상대 오프셋 기록
        foreach (var g in preplacedParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    void TransformToLiquid()
    {
        Vector2 center = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 inheritVel = inheritVelocity ? rb.velocity : Vector2.zero;

        // 변환 중 자기 충돌/시각 비활성화
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        rb.simulated = false;

        if (preplacedParticles.Count > 0)
        {
            // 경로 1: 미리 배치한 입자 재배치/활성
            for (int i = 0; i < preplacedParticles.Count; i++)
            {
                var g = preplacedParticles[i];
                if (!g) continue;

                // 비활성 부모 아래 있으면 월드로 분리
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
            // 경로 2: 프리팹 생성
            if (!liquidParticlePrefab)
            {
                Debug.LogWarning("liquidParticlePrefab이 비어있고, preplacedParticles도 없습니다.");
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

        // 고체 제거
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
