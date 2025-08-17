// LiquidToSolidRemorph2D.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidToSolid : MonoBehaviour
{
    [Header("액체 입자 부모(폴더) 또는 개별 입자들 (섞어 넣어도 됨)")]
    public List<GameObject> liquidRootsOrParticles = new List<GameObject>();

    [Header("태그 기반 동적 수집 (옵션)")]
    public bool includeTagSearch = true;
    public string liquidTag = "Liquid";   // 액체 프리팹/인스턴스에 이 태그를 달아두면 자동 수집됨

    [Header("고체 프리팹 (반드시 지정)")]
    public GameObject solidPrefab;

    [Header("키 / 모이기 연출")]
    public KeyCode toSolidKey = KeyCode.C;
    public float gatherDuration = 0.6f;   // 모으는 연출 시간
    public float endRadius = 0.02f;       // 말미 잔떨림(0이면 완전 한 점)

    [Header("복원 위치/속도 옵션")]
    public bool alignToGround = true;     // 바닥에 맞춰 살짝 띄워 배치
    public LayerMask groundLayer;
    public float solidRadius = 0.5f;      // 원 고체 반지름(바닥에서 이만큼 위로 올림)
    public bool inheritAverageVelocity = true; // 입자 평균속도를 고체 초기속도로

    [Header("정리 방식")]
    public bool destroyParticlesOnSolidify = false; // true면 합체 후 입자 Destroy, false면 비활성

    bool busy;

    // 실행 시점에 “현재” 액티브 입자를 모으는 함수(부모/개별/태그 모두 지원)
    void ResolveParticlesRuntime(List<GameObject> outList)
    {
        outList.Clear();
        var set = new HashSet<GameObject>();

        // 부모/개별에서 수집
        foreach (var root in liquidRootsOrParticles)
        {
            if (!root) continue;

            // 자기 자신이 입자면 추가
            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            // 자식 중 Rigidbody2D를 가진 액티브/비활성 포함
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs)
                if (r) set.Add(r.gameObject);
        }

        // 태그 기반 수집(옵션)
        if (includeTagSearch && !string.IsNullOrEmpty(liquidTag))
        {
            var gos = GameObject.FindGameObjectsWithTag(liquidTag);
            foreach (var go in gos)
                if (go && go.TryGetComponent<Rigidbody2D>(out _))
                    set.Add(go);
        }

        // 최종 정리 (현재 씬에서 활성인 애들만 실제로 사용)
        foreach (var g in set)
            if (g && g.activeInHierarchy) outList.Add(g);
    }

    void Update()
    {
        if (!busy && Input.GetKeyDown(toSolidKey))
            StartCoroutine(CoToSolid());
    }

    IEnumerator CoToSolid()
    {
        busy = true;

        // 1) 현재 활성 액체 입자 동적 수집
        var active = new List<GameObject>(64);
        ResolveParticlesRuntime(active);

        if (active.Count == 0)
        {
            Debug.LogWarning("[LiquidToSolid] 활성 액체 입자를 찾지 못했어요.");
            busy = false;
            yield break;
        }

        // 2) 센트로이드/평균속도 계산
        Vector2 sumPos = Vector2.zero, sumVel = Vector2.zero;
        int n = 0;
        foreach (var g in active)
        {
            var tr = g.transform;
            sumPos += (Vector2)tr.position;
            var rb = g.GetComponent<Rigidbody2D>();
            if (rb) sumVel += rb.velocity;
            n++;
        }
        Vector2 center = sumPos / n;
        Vector2 avgVel = (n > 0) ? (sumVel / n) : Vector2.zero;

        // 바닥 정렬 옵션: 센터 아래로 레이캐스트
        if (alignToGround)
        {
            var hit = Physics2D.Raycast(center + Vector2.up * 0.2f, Vector2.down, 5f, groundLayer);
            if (hit.collider)
                center = hit.point + hit.normal.normalized * solidRadius;
        }

        // 3) 모으는 동안 물리 잠시 끔 + 시작 위치 저장
        var starts = new Vector3[active.Count];
        var saved = new List<(Rigidbody2D rb, bool simulated, bool kinematic)>(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            var go = active[i];
            starts[i] = go.transform.position;

            var rb2d = go.GetComponent<Rigidbody2D>();
            if (rb2d)
            {
                saved.Add((rb2d, rb2d.simulated, rb2d.isKinematic));
                rb2d.velocity = Vector2.zero;
                rb2d.angularVelocity = 0f;
                // 시뮬레이션을 끄면 충돌없이 위치만 보간 가능
                rb2d.simulated = false;
            }
        }

        // 4) 부드럽게 무게중심으로 모으기 (smoothstep)
        float t = 0f;
        while (t < gatherDuration)
        {
            float s = t / gatherDuration;
            float u = s * s * (3f - 2f * s); // smoothstep 0->1

            for (int i = 0; i < active.Count; i++)
            {
                var g = active[i];
                if (!g) continue;

                Vector2 p = Vector2.Lerp((Vector2)starts[i], center, u);

                if (endRadius > 0f)
                {
                    // 입자마다 다른 위상으로 미세 흔들림
                    float seed = (i * 0.6180339887f) % 1f;
                    float ang = seed * Mathf.PI * 2f;
                    Vector2 jitter = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (endRadius * (1f - u));
                    p += jitter;
                }

                var pos = g.transform.position;
                g.transform.position = new Vector3(p.x, p.y, pos.z);
            }

            t += Time.deltaTime;
            yield return null;
        }

        // 5) 스냅 + 입자 정리(비활성 또는 파괴) + 물리 복구
        for (int i = 0; i < active.Count; i++)
        {
            var g = active[i];
            if (!g) continue;

            var pos = g.transform.position;
            g.transform.position = new Vector3(center.x, center.y, pos.z);

            if (destroyParticlesOnSolidify) Object.Destroy(g);
            else g.SetActive(false);
        }
        foreach (var s in saved)
        {
            if (!s.rb) continue;
            s.rb.isKinematic = s.kinematic;
            s.rb.simulated = s.simulated;
        }

        // 6) 고체 스폰
        if (!solidPrefab)
        {
            Debug.LogError("[LiquidToSolidRemorph2D] solidPrefab이 비어있습니다.");
        }
        else
        {
            var solid = Instantiate(solidPrefab, center, Quaternion.identity);

            // 평균 속도 상속(선택)
            if (inheritAverageVelocity)
            {
                var srb = solid.GetComponent<Rigidbody2D>();
                if (srb) srb.velocity = avgVel;
            }
        }

        busy = false;
    }
}
