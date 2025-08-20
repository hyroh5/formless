using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidGasSwitcher2D : MonoBehaviour
{
    public enum Phase { Liquid, Gas }

    [Header("세트 루트(부모 폴더) 또는 개별 파티클들")]
    public List<GameObject> liquidRootsOrParticles = new();
    public List<GameObject> gasRootsOrParticles = new();

    [Header("디자인 기준점(비우면 각 세트의 초기 무게중심)")]
    public Transform designCenter;

    [Header("전환 설정")]
    public float initialKick = 2f;
    public bool inheritVelocity = true;

    [Header("키(디렉터 없이 단독 사용 시)")]
    public bool listenHotkeys = false;
    public KeyCode toLiquidKey = KeyCode.Alpha2;
    public KeyCode toGasKey = KeyCode.Alpha3;

    [Header("전환 연출")]
    public float blendDurationGL = 0.4f;   // Gas→Liquid 블렌드 시간
    public bool smoothDampVelGL = true;   // 속도 감속 여부

    [Header("현재 상태")]
    public Phase current = Phase.Liquid;

    readonly List<GameObject> liquidLeafs = new();
    readonly List<GameObject> gasLeafs = new();
    readonly List<Vector2> liquidOffsets = new();
    readonly List<Vector2> gasOffsets = new();
    readonly Dictionary<Transform, Vector3> originalScales = new();
    public void ForceSetToLiquid() => ForceSetTo(Phase.Liquid);
    public void ForceSetToGas() => ForceSetTo(Phase.Gas);

    void Awake()
    {
        ResolveLeafs(liquidRootsOrParticles, liquidLeafs);
        ResolveLeafs(gasRootsOrParticles, gasLeafs);

        CacheOriginalScales(liquidLeafs);
        CacheOriginalScales(gasLeafs);

        CaptureOffsets(liquidLeafs, liquidOffsets);
        CaptureOffsets(gasLeafs, gasOffsets);

        StrictSet(liquidRootsOrParticles, current == Phase.Liquid);
        StrictSet(gasRootsOrParticles, current == Phase.Gas);
    }

    void Update()
    {
        if (!listenHotkeys) return;

        if (Input.GetKeyDown(toLiquidKey))
        {
            if (current == Phase.Gas) Switch_GasToLiquid();
            else Switch_LiquidToLiquid();
        }
        else if (Input.GetKeyDown(toGasKey))
        {
            if (current == Phase.Liquid) Switch_LiquidToGas();
            else Switch_GasToGas();
        }
    }

    public void Switch_LiquidToGas()
    {
        if (current == Phase.Gas) return;
        Switch(liquidLeafs, gasLeafs, liquidOffsets, gasOffsets, Phase.Gas, false);
    }
    public void Switch_GasToLiquid()
    {
        if (current == Phase.Liquid) return;
        Switch(gasLeafs, liquidLeafs, gasOffsets, liquidOffsets, Phase.Liquid, false);
    }
    public void Switch_LiquidToLiquid()
    {
        if (current == Phase.Liquid) return;
        Switch(liquidLeafs, liquidLeafs, liquidOffsets, liquidOffsets, Phase.Liquid, true);
    }
    public void Switch_GasToGas()
    {
        if (current == Phase.Gas) return;
        Switch(gasLeafs, gasLeafs, gasOffsets, gasOffsets, Phase.Gas, true);
    }

    void Switch(
    List<GameObject> fromLeafs,
    List<GameObject> toLeafs,
    List<Vector2> fromOffsets,
    List<Vector2> toOffsets,
    Phase targetPhase,
    bool sameSet
)
    {
        // 1) 기존 평균속도/센터 계산(속도 상속 등 기존 로직 유지용)
        ComputeCenterAndAvgVel(fromLeafs, out var fromCenter, out var fromAvgVel);

        // ★ 추가: 'from'에서 현재 활성 파티클들의 위치 수집
        var fromPositions = new List<Vector2>(toLeafs.Count);
        GetActivePositions(fromLeafs, fromPositions);

        // 2) from 비활성화 → to 활성화(기존)
        StrictDeactivate(fromLeafs);
        ForceActivateAncestors(toLeafs);
        SetHierarchyActive(toLeafs, true);

        // ★ 추가: 1:1 매칭 테이블 구성 (가까운 from 위치를 to에 할당)
        var map = new List<int>(toLeafs.Count);
        if (fromPositions.Count > 0)
            BuildNearestMapping(fromPositions, toLeafs.Count, map);

        // 3) to 파티클을 '센터+오프셋'이 아니라, 1:1 매칭된 from 위치로 배치
        for (int i = 0; i < toLeafs.Count; i++)
        {
            var g = toLeafs[i]; if (!g) continue;

            ToggleRenderers(g, false);
            ToggleColliders(g, false);
            ToggleRigidbodies(g, false);

            Vector2 spawnPos;
            if (fromPositions.Count > 0 && i < map.Count)
                spawnPos = fromPositions[map[i]];
            else
            {
                // from 위치가 없으면(안전장치) 예전 방식 유지
                var off = (i < toOffsets.Count) ? toOffsets[i] : Vector2.zero;
                spawnPos = fromCenter + off;
            }

            g.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
            g.transform.rotation = Quaternion.identity;
            RestoreScaleRecursive(g.transform);
        }

        // 4) 물리/렌더 on + 초기 속도(기존)
        for (int i = 0; i < toLeafs.Count; i++)
        {
            var g = toLeafs[i]; if (!g) continue;

            ToggleColliders(g, true);
            ToggleRenderers(g, true);
            ToggleRigidbodies(g, true);

            var rb = g.GetComponent<Rigidbody2D>();
            if (rb)
            {
                // 방향은 살짝 랜덤(or 오프셋 기반) + 평균속도 상속
                var dir = Random.insideUnitCircle.normalized;
                var inherit = inheritVelocity ? fromAvgVel : Vector2.zero;
                rb.velocity = inherit + dir * initialKick;
            }

            var pss = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss) if (ps && !ps.isPlaying) ps.Play();
        }

        // 5) (옵션) Gas→Liquid 블렌드 연출은 유지
        if (targetPhase == Phase.Liquid && !sameSet && blendDurationGL > 0f)
            StartCoroutine(CoBlendFromCurrentToOffsets(
                toLeafs, toOffsets, fromCenter, blendDurationGL, smoothDampVelGL));

        current = targetPhase;
    }


    IEnumerator CoBlendFromCurrentToOffsets(
     List<GameObject> leafs,
     List<Vector2> offsets,
     Vector2 center,
     float dur,
     bool smoothDampVel)
    {
        // 각 파티클의 시작 위치를 캐싱
        var starts = new Vector3[leafs.Count];
        for (int i = 0; i < leafs.Count; i++)
            starts[i] = leafs[i] ? leafs[i].transform.position : Vector3.zero;

        float t = 0f;
        while (t < dur)
        {
            float u = t / dur;
            // 스무스스텝(부드러운 가속/감속)
            u = u * u * (3f - 2f * u);

            for (int i = 0; i < leafs.Count; i++)
            {
                var g = leafs[i]; if (!g) continue;

                // 목표는 "센터 + 오프셋" (패턴 자리)
                Vector2 off = (i < offsets.Count) ? offsets[i] : Vector2.zero;
                Vector2 target = center + off;

                // ★ 현재 위치(starts[i])에서 목표 위치까지 보간
                Vector2 p = Vector2.Lerp((Vector2)starts[i], target, u);
                g.transform.position = new Vector3(p.x, p.y, g.transform.position.z);

                if (smoothDampVel)
                {
                    var rb = g.GetComponent<Rigidbody2D>();
                    if (rb) rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, u);
                }
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 마지막 스냅 정리
        for (int i = 0; i < leafs.Count; i++)
        {
            var g = leafs[i]; if (!g) continue;
            Vector2 off = (i < offsets.Count) ? offsets[i] : Vector2.zero;
            Vector2 target = center + off;
            g.transform.position = new Vector3(target.x, target.y, g.transform.position.z);
        }
    }


    static void ResolveLeafs(List<GameObject> rootsOrParticles, List<GameObject> outLeafs)
    {
        outLeafs.Clear();
        var set = new HashSet<GameObject>();
        foreach (var root in rootsOrParticles)
        {
            if (!root) continue;
            if (root.TryGetComponent<Rigidbody2D>(out _)) set.Add(root);
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs) if (r) set.Add(r.gameObject);
        }
        outLeafs.AddRange(set);
    }

    void CaptureOffsets(List<GameObject> leafs, List<Vector2> outOffsets)
    {
        outOffsets.Clear();
        if (leafs.Count == 0) return;

        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in leafs) { if (!g) continue; sum += (Vector2)g.transform.position; n++; }
            center = (n > 0) ? sum / n : (Vector2)transform.position;
        }

        foreach (var g in leafs)
            outOffsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    static void ComputeCenterAndAvgVel(List<GameObject> leafs, out Vector2 center, out Vector2 avgVel)
    {
        if (leafs == null || leafs.Count == 0) { center = Vector2.zero; avgVel = Vector2.zero; return; }
        Vector2 sumPos = Vector2.zero, sumVel = Vector2.zero; int n = 0;
        foreach (var g in leafs)
        {
            if (!g) continue;
            sumPos += (Vector2)g.transform.position;
            var rb = g.GetComponent<Rigidbody2D>();
            if (rb) sumVel += rb.velocity;
            n++;
        }
        center = (n > 0) ? sumPos / n : Vector2.zero;
        avgVel = (n > 0) ? sumVel / n : Vector2.zero;
    }

    void CacheOriginalScales(List<GameObject> leafs)
    {
        foreach (var g in leafs)
        {
            if (!g) continue;
            var all = g.GetComponentsInChildren<Transform>(true);
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

    static void SetHierarchyActive(List<GameObject> items, bool on)
    {
        foreach (var it in items) SetHierarchyActive(it, on);
    }
    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }

    static void StrictDeactivate(List<GameObject> leafs)
    {
        foreach (var g in leafs)
        {
            if (!g) continue;
            ToggleRenderers(g, false);
            ToggleColliders(g, false);
            ToggleRigidbodies(g, false);
        }
        var roots = new HashSet<GameObject>();
        foreach (var g in leafs)
            if (g) roots.Add(GetTop(g.transform).gameObject);
        foreach (var root in roots) SetHierarchyActive(root, false);

        static Transform GetTop(Transform t)
        {
            while (t.parent != null) t = t.parent;
            return t;
        }
    }

    static void ForceActivateAncestors(List<GameObject> leafs)
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

    void StrictSet(List<GameObject> rootsOrParticles, bool on)
    {
        var tmpLeafs = new List<GameObject>();
        ResolveLeafs(rootsOrParticles, tmpLeafs);
        if (on)
        {
            ForceActivateAncestors(tmpLeafs);
            SetHierarchyActive(rootsOrParticles, true);
        }
        else
        {
            StrictDeactivate(tmpLeafs);
        }
    }
    
    void ForceSetTo(Phase p)
    {
        if (p == Phase.Liquid)
        {
            StrictSet(gasRootsOrParticles, false);
            StrictSet(liquidRootsOrParticles, true);
            current = Phase.Liquid;
        }
        else
        {
            StrictSet(liquidRootsOrParticles, false);
            StrictSet(gasRootsOrParticles, true);
            current = Phase.Gas;
        }
    }

    // fromLeafs 중 현재 활성인 것들의 '월드 위치'만 뽑음
    static void GetActivePositions(List<GameObject> leafs, List<Vector2> outPositions)
    {
        outPositions.Clear();
        foreach (var g in leafs)
            if (g && g.activeInHierarchy)
                outPositions.Add(g.transform.position);
    }

    // toLeafs 각각에 대해 가장 가까운 from 위치를 하나씩 할당(중복 방지, 그리디)
    static void BuildNearestMapping(List<Vector2> fromPositions, int toCount, List<int> outMap)
    {
        outMap.Clear();
        var used = new bool[fromPositions.Count];

        for (int i = 0; i < toCount; i++)
        {
            int best = -1;
            float bestD = float.PositiveInfinity;

            for (int j = 0; j < fromPositions.Count; j++)
            {
                if (used[j]) continue;
                float d = (fromPositions[j] - (fromPositions.Count > 0 ? fromPositions[Mathf.Min(i, fromPositions.Count - 1)] : Vector2.zero)).sqrMagnitude;
                // ↑ 기준점 없이도 충분히 잘 동작. 더 정교하게 하려면 toOffsets[i] 등을 참조해도 됨.
                if (d < bestD) { bestD = d; best = j; }
            }

            if (best < 0) best = Mathf.Clamp(i, 0, fromPositions.Count - 1); // 안전장치
            used[best] = true;
            outMap.Add(best);
        }
    }

}



