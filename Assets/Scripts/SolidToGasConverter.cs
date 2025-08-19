using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToGasConverter : MonoBehaviour
{
    [Header("가스 루트(부모 폴더) 또는 개별 입자들(둘 다 허용)")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("원 중심(비우면 초기 무게중심 사용)")]
    public Transform designCenter;

    [Header("변환 설정")]
    public float initialForce = 2f;     // 0이면 퍼지는 힘 없음
    public bool listenHotkey = true;    // 키로 변환 트리거할지
    public KeyCode convertKey = KeyCode.X;

    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;               // 고체 외형만 숨김
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>(true);

        CapturePattern(); // 가스 입자 패턴 기억
    }

    void Start()
    {
        // 시작 시 가스 전부 OFF
        foreach (var root in gasParticles)
            SetHierarchyActive(root, false);
    }

    void Update()
    {
        if (listenHotkey && Input.GetKeyDown(convertKey))
            ConvertToGas();
    }

    // 외부에서 호출 가능
    public void ConvertToGas()
    {
        Vector2 spawnCenter = col ? (Vector2)col.bounds.center : (Vector2)transform.position;

        // 고체 숨김 + 물리 차단 (movement 의존성 제거)
        if (renderers != null) foreach (var r in renderers) if (r) r.enabled = false;
        if (rb)  rb.simulated = false;
        if (col) col.enabled    = false;

        // 가스 트리 전체 ON + 보이기 보장 + 배치/속도
        var allGasLeafs = ResolveAllLeafs(); // 실제 파티클(자식들 포함) 목록

        // 부모 트리 자체도 반드시 ON (두 번째 X 대비)
        foreach (var root in gasParticles)
            SetHierarchyActive(root, true);

        for (int i = 0; i < allGasLeafs.Count; i++)
        {
            var g = allGasLeafs[i];
            if (!g) continue;

            // 렌더러/파티클/알파 ON
            var rends = g.GetComponentsInChildren<Renderer>(true);
            foreach (var rr in rends) if (rr) rr.enabled = true;

            var pss = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss) if (ps && !ps.isPlaying) ps.Play();

            var srs = g.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
                if (sr) { var c = sr.color; c.a = 1f; sr.color = c; }

            // 위치/속도
            Vector2 off = i < offsets.Count ? offsets[i] : Vector2.zero;
            g.transform.position = spawnCenter + off;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Vector2.zero;
                grb.velocity = dir * initialForce;
            }
        }
    }

    // 초기 디자인 패턴(상대 좌표) 저장
    void CapturePattern()
    {
        offsets.Clear();

        var leafs = ResolveAllLeafs();
        if (leafs.Count == 0) return;

        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in leafs)
            {
                if (!g) continue;
                sum += (Vector2)g.transform.position; n++;
            }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }

        foreach (var g in leafs)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    // 현재 설정된 루트들로부터 "실제 파티클 후보" 추출
    List<GameObject> ResolveAllLeafs()
    {
        var list = new List<GameObject>();
        var set = new HashSet<GameObject>();

        foreach (var root in gasParticles)
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

    // 외부에서 안전하게 접근용
    public bool HasAnyGas() => gasParticles != null && gasParticles.Count > 0;
    public IEnumerable<GameObject> GetGasRootsOrParticlesSafe() => gasParticles ?? (IEnumerable<GameObject>)System.Array.Empty<GameObject>();

    // 부모/자식 포함 전체 토글
    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }
}