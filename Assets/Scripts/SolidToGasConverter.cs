using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToGasConverter : MonoBehaviour
{
    [Header("가스 루트(부모 폴더) 또는 개별 입자들")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("원 중심(디자인 기준)")]
    public Transform designCenter;

    [Header("퍼짐/힘 설정")]
    public float spawnJitter = 0.35f;      // 스폰 위치 난수 반경
    public float minKick = 1.8f;       // 임펄스 최소
    public float maxKick = 3.0f;       // 임펄스 최대
    public float upwardBias = 0.8f;       // 위쪽 가중(0~1 정도 추천)
    public float randomTorque = 60f;        // 초기 회전 속도
    public bool inheritVelocityFromSolid = true;

    [Header("입력")]
    public bool listenHotkey = true;
    public KeyCode convertKey = KeyCode.X;

    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;
    readonly List<Vector2> offsets = new List<Vector2>();

    static void ForceActivateAncestors(IEnumerable<GameObject> leafs)
    {
        var toActivate = new HashSet<GameObject>();
        foreach (var g in leafs)
        {
            if (!g) continue;
            for (var a = g.transform; a != null; a = a.parent)
                if (!a.gameObject.activeSelf) toActivate.Add(a.gameObject);
        }
        foreach (var go in toActivate) SetHierarchyActive(go, true);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>(true);
        CapturePattern();
    }

    void Start()
    {
        foreach (var root in gasParticles)
            SetHierarchyActive(root, false);
    }

    void Update()
    {
        if (listenHotkey && Input.GetKeyDown(convertKey))
            ConvertToGas();
    }

    public void ConvertToGas()
    {
        Vector2 center = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 baseVel = (inheritVelocityFromSolid && rb) ? rb.velocity : Vector2.zero;

        // 고체 숨기기
        if (renderers != null) foreach (var r in renderers) if (r) r.enabled = false;
        if (rb) rb.simulated = false;
        if (col) col.enabled = false;

        // ★ (1) 루트들을 먼저 ON (부모 트리부터 살리기)
        foreach (var root in gasParticles)
            SetHierarchyActive(root, true);

        // ★ (2) 그 다음 leafs 수집
        var leafs = ResolveAllLeafs();

        // ★ (3) 혹시 직접 넣은 개별 파티클만 있고 그 부모가 꺼져 있을 수 있으니 조상까지 ON
        ForceActivateAncestors(leafs);

        if (leafs.Count == 0)
        {
            Debug.LogWarning("[SolidToGasConverter] 활성화할 가스 입자를 찾지 못했습니다. gasParticles 리스트에 '씬 인스턴스'가 들어있는지 확인하세요 (프리팹 에셋 X).");
            return;
        }

        for (int i = 0; i < leafs.Count; i++)
        {
            var g = leafs[i];
            if (!g) continue;

            // 렌더 / 콜라이더 / 물리 ON
            foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;

            // ★ 알파 복구 (예전에 0으로 꺼놨을 가능성 있음)
            foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (!sr) continue;
                var c = sr.color; c.a = 1f; sr.color = c;
            }

            foreach (var c2 in g.GetComponentsInChildren<Collider2D>(true)) if (c2) c2.enabled = true;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                grb.isKinematic = false;
                grb.simulated = true;
            }

            // 위치: 중심 주변 난수
            Vector2 off = (i < offsets.Count) ? offsets[i] : Random.insideUnitCircle;
            Vector2 spawn = center + Random.insideUnitCircle * spawnJitter;
            g.transform.position = new Vector3(spawn.x, spawn.y, g.transform.position.z);
            g.transform.rotation = Quaternion.identity;

            // 초기 임펄스
            if (grb)
            {
                Vector2 dir = (off.sqrMagnitude > 1e-6f) ? off.normalized : Random.insideUnitCircle.normalized;
                dir = (dir + Vector2.up * Mathf.Clamp01(upwardBias)).normalized;

                float kick = Random.Range(minKick, maxKick);
                grb.velocity = baseVel;
                grb.AddForce(dir * kick, ForceMode2D.Impulse);
                grb.angularVelocity = Random.Range(-randomTorque, randomTorque);
            }

            // 파티클 재생
            var pss = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss) if (ps && !ps.isPlaying) ps.Play();
        }
    }



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
            foreach (var g in leafs) { if (!g) continue; sum += (Vector2)g.transform.position; n++; }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }
        foreach (var g in leafs)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    List<GameObject> ResolveAllLeafs()
    {
        var list = new List<GameObject>();
        var set = new HashSet<GameObject>();

        foreach (var root in gasParticles)
        {
            if (!root) continue;

            // 1) 자기 자신
            if (root.GetComponent<Rigidbody2D>() || root.GetComponent<Renderer>())
                set.Add(root);

            // 2) 자식들(비활성 포함)
            foreach (var r in root.GetComponentsInChildren<Rigidbody2D>(true))
                if (r) set.Add(r.gameObject);

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
                if (rend) set.Add(rend.gameObject);
        }

        list.AddRange(set);
        return list;
    }


    public bool HasAnyGas() => gasParticles != null && gasParticles.Count > 0;
    public IEnumerable<GameObject> GetGasRootsOrParticlesSafe() => gasParticles ?? (IEnumerable<GameObject>)System.Array.Empty<GameObject>();

    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }
}
