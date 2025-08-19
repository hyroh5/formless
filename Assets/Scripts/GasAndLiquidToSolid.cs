using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PhaseToSolidRealtime : MonoBehaviour
{
    [Header("액체/기체: 씬에 '미리 배치'된 입자 루트(부모) 또는 개별 파티클들")]
    public List<GameObject> liquidRootsOrParticles = new List<GameObject>();
    public List<GameObject> gasRootsOrParticles    = new List<GameObject>();

    [Header("키 / 모이기 연출")]
    public KeyCode toSolidKey = KeyCode.Alpha1;  // ← 1번키
    public float gatherDuration = 0.6f;
    public float endRadius = 0.02f;

    [Header("복원 옵션")]
    public bool alignToGround = true;
    public LayerMask groundLayer;
    public float solidRadius = 0.5f;
    public bool inheritAverageVelocity = true;

    [Header("합체 후 정리 방식")]
    public bool destroyLiquidOnSolidify = false;
    public bool destroyGasOnSolidify = false;

    [Header("고체 스폰 지연")]
    public float solidSpawnDelay = 0.0f;      // 0이면 바로
    public bool spawnDelayUnscaled = false;   // true면 Time.timeScale 무시

    // ---- 내부 ----
    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;
    bool busy;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    void Update()
    {
        if (!busy && Input.GetKeyDown(toSolidKey))
            StartCoroutine(CoToSolidAuto());
    }

    IEnumerator CoToSolidAuto()
    {
        busy = true;

        // 합체 연출 동안 고체는 숨겨두기(지연 체감/위치 고정)
        HideSolid();

        // 1) 양쪽에서 활성 입자 수집
        var activeLiquid = CollectActiveParticles(liquidRootsOrParticles);
        var activeGas    = CollectActiveParticles(gasRootsOrParticles);

        // 2) 어느 쪽을 합칠지 결정 (액체 우선)
        List<GameObject> active = null;
        bool isLiquid = false;

        if (activeLiquid.Count > 0)
        {
            active = activeLiquid;
            isLiquid = true;
        }
        else if (activeGas.Count > 0)
        {
            active = activeGas;
            isLiquid = false;
        }

        // 3) 활성 입자 없으면 현재 위치에서 바로 복구
        if (active == null || active.Count == 0)
        {
            yield return Delay();
            RestoreSolid(transform.position, Vector2.zero);
            busy = false;
            yield break;
        }

        // 4) 무게중심/평균속도 계산
        Vector2 center = Vector2.zero, sumVel = Vector2.zero;
        for (int i = 0; i < active.Count; i++)
        {
            var tr = active[i].transform;
            center += (Vector2)tr.position;

            var prb = active[i].GetComponent<Rigidbody2D>();
            if (prb) sumVel += prb.velocity;
        }
        center /= active.Count;
        Vector2 avgVel = sumVel / Mathf.Max(1, active.Count);

        // 바닥 정렬(옵션)
        if (alignToGround)
        {
            var hit = Physics2D.Raycast(center + Vector2.up * 0.2f, Vector2.down, 5f, groundLayer);
            if (hit.collider)
                center = hit.point + hit.normal.normalized * solidRadius;
        }

        // 5) 모으는 동안 물리 잠깐 OFF + 시작 위치 저장
        var starts = new Vector3[active.Count];
        var saved = new List<(Rigidbody2D rb2, bool simulated, float angVel)>(active.Count);

        for (int i = 0; i < active.Count; i++)
        {
            starts[i] = active[i].transform.position;
            var rb2d = active[i].GetComponent<Rigidbody2D>();
            if (rb2d)
            {
                saved.Add((rb2d, rb2d.simulated, rb2d.angularVelocity));
                rb2d.velocity = Vector2.zero;
                rb2d.angularVelocity = 0f;
                rb2d.simulated = false;
            }
        }

        // 6) 스무스하게 무게중심으로 모으기
        float t = 0f;
        while (t < gatherDuration)
        {
            float s = t / gatherDuration;
            float u = s * s * (3f - 2f * s); // smoothstep

            for (int i = 0; i < active.Count; i++)
            {
                var p = Vector2.Lerp((Vector2)starts[i], center, u);

                if (endRadius > 0f)
                {
                    float seed = (i * 0.6180339887f) % 1f;
                    float ang = seed * Mathf.PI * 2f;
                    Vector2 jitter = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (endRadius * (1f - u));
                    p += jitter;
                }

                var pos = active[i].transform.position;
                active[i].transform.position = new Vector3(p.x, p.y, pos.z);
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 7) 스냅 + 입자 끄기/삭제 + 물리 복구
        for (int i = 0; i < active.Count; i++)
        {
            var go = active[i];
            var pos = go.transform.position;
            go.transform.position = new Vector3(center.x, center.y, pos.z);

            if ((isLiquid && destroyLiquidOnSolidify) || (!isLiquid && destroyGasOnSolidify))
                Object.Destroy(go);
            else
                go.SetActive(false);
        }
        foreach (var s in saved)
        {
            if (!s.rb2) continue;
            s.rb2.angularVelocity = s.angVel;
            s.rb2.simulated = s.simulated;
        }

        // 8) 사용한 쪽 트리 전체 OFF 보장 (부모까지 확실히 내리기)
        if (isLiquid) ForceSetHierarchy(liquidRootsOrParticles, false);
        else          ForceSetHierarchy(gasRootsOrParticles,    false);

        // 9) 딜레이 후, "무게중심"에서 고체 복구
        Vector3 finalCenter = new Vector3(center.x, center.y, transform.position.z);
        yield return Delay();
        RestoreSolid(finalCenter, avgVel);

        busy = false;
    }

    // ---- 유틸리티들 ----

    List<GameObject> CollectActiveParticles(List<GameObject> rootsOrParticles)
    {
        var outList = new List<GameObject>(64);
        var set = new HashSet<GameObject>();

        foreach (var root in rootsOrParticles)
        {
            if (!root) continue;

            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs) if (r) set.Add(r.gameObject);
        }

        foreach (var g in set)
            if (g && g.activeInHierarchy) outList.Add(g);

        return outList;
    }

    void ForceSetHierarchy(List<GameObject> roots, bool on)
    {
        foreach (var root in roots) SetHierarchyActive(root, on);
    }

    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }

    void HideSolid()
    {
        if (renderers != null)
            foreach (var r in renderers) if (r) r.enabled = false;

        if (rb)  rb.simulated = false;
        if (col) col.enabled = false;
    }

    void RestoreSolid(Vector3 pos, Vector2 vel)
    {
        transform.position = pos;

        if (renderers != null)
            foreach (var r in renderers) if (r) r.enabled = true;

        if (rb)
        {
            rb.simulated = true;
            if (inheritAverageVelocity) rb.velocity = vel;
        }
        if (col) col.enabled = true;
    }

    IEnumerator Delay()
    {
        if (solidSpawnDelay <= 0f) yield break;
        if (spawnDelayUnscaled) yield return new WaitForSecondsRealtime(solidSpawnDelay);
        else                    yield return new WaitForSeconds(solidSpawnDelay);
    }
}






