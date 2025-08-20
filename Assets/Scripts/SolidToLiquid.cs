// SolidToLiquid2D.cs  — 고체 이동/점프 포함 + 두 번째부터 깜빡임 해결(두-패스 + 스케일 복구)
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquid2D : MonoBehaviour
{
    [Header("씬에 미리 배치한 액체 입자들(부모 또는 개별)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("디자인 기준점(비우면 입자 평균위치/자기 위치)")]
    public Transform designCenter;

    [Header("변환 시 초기 힘")]
    public float initialForce = 2f;       // 0이면 퍼지는 힘 없음
    public bool inheritVelocity = true;   // 고체 속도 상속

    [Header("키 설정")]
    public KeyCode morphKey = KeyCode.M;  // 디렉터에서 None으로 덮어씀

    // ===== 고체 이동/점프 =====
    [Header("이동/점프")]
    public float moveForce = 20f;
    public float maxSpeed = 6f;
    public float jumpVelocity = 12f;
    public Transform groundCheck;
    public float groundCheckRadius = 0.20f;
    public LayerMask groundLayer;
    [SerializeField] float coyoteTime = 0.1f;
    [SerializeField] float jumpBuffer = 0.12f;

    // ---- 내부 ----
    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer[] cachedRenderers;

    float moveInput;
    bool isGrounded;
    float lastGroundTime = -999f;
    float lastJumpPressTime = -999f;

    // 오프셋(입자 상대 위치), 스케일 복구 캐시
    readonly List<Vector2> offsets = new List<Vector2>();
    readonly Dictionary<Transform, Vector3> originalScales = new Dictionary<Transform, Vector3>();

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        CacheOriginalScales();
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
        // 시작 시 액체는 모두 비활성(재사용 전제)
        ForceSetHierarchyAll(preplacedParticles, false);
    }

    void Update()
    {
        if (rb.simulated)
        {
            // 좌우 입력(화살표 전용)
            moveInput =
                (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) +
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f);

            // 점프 입력 버퍼
            if (Input.GetKeyDown(KeyCode.Space))
                lastJumpPressTime = Time.time;

            // 상태 변환
            if (Input.GetKeyDown(morphKey))
                TransformToLiquid();
        }
        else
        {
            // 액체 상태: 이동입력 무시, 변환키는 허용(원하면)
            moveInput = 0f;
            if (Input.GetKeyDown(morphKey))
                TransformToLiquid();
        }
    }

    void FixedUpdate()
    {
        if (!rb.simulated) return;

        // 접지 판정
        isGrounded = IsGrounded();
        if (isGrounded) lastGroundTime = Time.time;

        // 힘 기반 이동
        if (Mathf.Abs(rb.velocity.x) < maxSpeed || Mathf.Sign(moveInput) != Mathf.Sign(rb.velocity.x))
            rb.AddForce(new Vector2(moveInput * moveForce, 0f), ForceMode2D.Force);

        // 점프: 버퍼 + 코요테타임
        bool canCoyote   = Time.time - lastGroundTime   <= coyoteTime;
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
        // 1) groundCheck 원형 오버랩
        bool circ = groundCheck && Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
        if (circ) return true;

        // 2) 접촉 법선 검사
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

        // preplaced 목록 아래 실제 파티클(=Rigidbody2D 보유)들을 수집해, 그 순서 기준으로 오프셋 저장
        var leafs = ResolveAllLeafs();
        if (leafs.Count == 0) return;

        // 기준점 계산
        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in leafs) { if (!g) continue; sum += (Vector2)g.transform.position; n++; }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }

        // 상대 오프셋 기록 (leafs 순서)
        foreach (var g in leafs)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    // ▶ 부모/조상 강제 ON + 전체 토글
    static void ForceActivateAllAncestorsOf(List<GameObject> particles)
    {
        var toActivate = new HashSet<GameObject>();
        foreach (var g in particles)
        {
            if (!g) continue;
            for (var a = g.transform; a != null; a = a.parent)
                if (!a.gameObject.activeSelf) toActivate.Add(a.gameObject);
        }
        foreach (var go in toActivate) SetHierarchyActive(go, true);
    }

    static void ForceSetHierarchyAll(List<GameObject> particles, bool on)
    {
        foreach (var g in particles)
            SetHierarchyActive(g, on);
    }

    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }

    // ====== '두-패스' 전환 ======
    void TransformToLiquid()
    {
        Vector2 center      = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 inheritVel  = inheritVelocity ? rb.velocity : Vector2.zero;

        // (1) 고체 숨김 & 물리 OFF
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        rb.simulated = false;
        if (cachedRenderers != null)
            foreach (var sr in cachedRenderers) if (sr) sr.enabled = false;

        // (2) 실제 파티클(자식 포함) 수집 + 부모/조상 확실히 ON
        var leafs = ResolveAllLeafs();            // 일관 순서
        ForceActivateAllAncestorsOf(preplacedParticles);
        ForceSetHierarchyAll(preplacedParticles, true);

        // (3) 패스 A — 보이지 않는 상태에서 자리 먼저 잡기
        for (int i = 0; i < leafs.Count; i++)
        {
            var g = leafs[i];
            if (!g) continue;

            ToggleRenderers(g, false);
            ToggleColliders(g, false);
            ToggleRigidbodies(g, false);

            Vector2 off = (i < offsets.Count) ? offsets[i] : Vector2.zero;
            var pos = center + off;

            g.transform.position = new Vector3(pos.x, pos.y, 0f);
            g.transform.rotation = Quaternion.identity;
            RestoreScaleRecursive(g.transform);
        }

        // (4) 패스 B — 같은 프레임에서 보이기 + 물리 켜기 + 초기 힘
        for (int i = 0; i < leafs.Count; i++)
        {
            var g = leafs[i];
            if (!g) continue;

            ToggleColliders(g, true);
            ToggleRenderers(g, true);
            ToggleRigidbodies(g, true);

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                Vector2 off = (i < offsets.Count) ? offsets[i] : Vector2.zero;
                Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Random.insideUnitCircle.normalized;
                grb.velocity = inheritVel + dir * initialForce;
            }

            var pss = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss) if (ps && !ps.isPlaying) ps.Play();
        }
    }

    // 실제 파티클 후보(해당 루트들 아래 Rigidbody2D 가진 오브젝트들)
    List<GameObject> ResolveAllLeafs()
    {
        var list = new List<GameObject>();
        var set  = new HashSet<GameObject>();

        foreach (var root in preplacedParticles)
        {
            if (!root) continue;

            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs) if (r) set.Add(r.gameObject);
        }

        list.AddRange(set);
        return list;
    }

    // ===== 유틸: 렌더/콜라이더/리짓바디 토글 + 스케일 캐시/복구 =====
    static void ToggleRenderers(GameObject g, bool on)
    {
        var rends = g.GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) if (r) r.enabled = on;

        var srs = g.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs)
            if (sr) { var c = sr.color; c.a = on ? 1f : 0f; sr.color = c; }
    }

    static void ToggleColliders(GameObject g, bool on)
    {
        var cols = g.GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols) if (c) c.enabled = on;
    }

    static void ToggleRigidbodies(GameObject g, bool simulated)
    {
        var rbs = g.GetComponentsInChildren<Rigidbody2D>(true);
        foreach (var r in rbs) if (r)
        {
            r.velocity = Vector2.zero;
            r.angularVelocity = 0f;
            r.simulated = simulated;
        }
    }

    void CacheOriginalScales()
    {
        originalScales.Clear();
        foreach (var root in preplacedParticles)
        {
            if (!root) continue;
            var all = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
                if (t && !originalScales.ContainsKey(t)) originalScales[t] = t.localScale;
        }
    }

    void RestoreScaleRecursive(Transform t)
    {
        if (!t) return;
        if (originalScales.TryGetValue(t, out var s)) t.localScale = s;
        for (int i = 0; i < t.childCount; i++)
            RestoreScaleRecursive(t.GetChild(i));
    }
}

