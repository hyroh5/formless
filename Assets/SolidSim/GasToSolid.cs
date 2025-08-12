using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class GasToSolidRealtime : MonoBehaviour
{
    [Header("기체 폴더(부모) 또는 개별 입자들 (섞어 넣어도 됨)")]
    public List<GameObject> gasRootsOrParticles = new List<GameObject>();

    [Header("키 / 모이기 시간")]
    public KeyCode toSolidKey = KeyCode.C;
    public float gatherDuration = 0.6f; // 부드럽게 모일 시간
    public float endRadius = 0.02f;     // 마감 잔떨림(0이면 한 점)

    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;
    bool busy;

    // ★ 부모 구조가 바뀌어도 안 깨지는 '확정 입자 목록'
    readonly List<GameObject> particles = new List<GameObject>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>(true);

        ResolveParticlesOnce(); // ★ 한 번만 전개해서 캐싱
    }

    // 에디터에서 목록 바꾸면 즉시 다시 전개
    void OnValidate()
    {
        if (Application.isPlaying) return;
        ResolveParticlesOnce();
    }

    void Update()
    {
        if (!busy && Input.GetKeyDown(toSolidKey))
            StartCoroutine(CoToSolid());
    }

    // --- 부모/개별 섞여 들어와도 실제 입자(Rigidbody2D 보유)만 캐싱 ---
    void ResolveParticlesOnce()
    {
        particles.Clear();
        var set = new HashSet<GameObject>();

        foreach (var root in gasRootsOrParticles)
        {
            if (!root) continue;

            // 자신이 입자라면 추가
            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            // 모든 자식 중 입자만 추가(비활성 포함)
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs)
                if (r) set.Add(r.gameObject);
        }

        // 사용자가 개별 입자만 넣는 경우도 커버(중복 제거됨)
        foreach (var g in gasRootsOrParticles)
            if (g && g.TryGetComponent<Rigidbody2D>(out _)) set.Add(g);

        particles.AddRange(set);
    }

    IEnumerator CoToSolid()
    {
        busy = true;

        // 1) 지금 '활성'인 입자만 모아 무게중심 계산
        var active = new List<GameObject>(particles.Count);
        Vector2 sum = Vector2.zero;
        foreach (var g in particles)
        {
            if (g && g.activeInHierarchy)
            {
                active.Add(g);
                sum += (Vector2)g.transform.position; // 월드 좌표
            }
        }
        if (active.Count == 0)
        {
            // 활성 입자가 없다면(이미 꺼져 있거나 못 찾았거나) 고체만 켠다
            RestoreSolid(transform.position);
            busy = false;
            yield break;
        }
        Vector2 center2D = sum / active.Count;

        // 2) 모으는 동안 입자 물리 끄고 시작 위치 저장
        var starts = new Vector3[active.Count];
        var savedSim = new List<(Rigidbody2D rb, bool was)>(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            starts[i] = active[i].transform.position;
            var grb = active[i].GetComponent<Rigidbody2D>();
            if (grb)
            {
                savedSim.Add((grb, grb.simulated));
                grb.velocity = Vector2.zero;
                grb.angularVelocity = 0f;
                grb.simulated = false;
            }
        }

        // 3) 부드럽게 무게중심으로 모으기 (smoothstep)
        float t = 0f;
        while (t < gatherDuration)
        {
            float s = t / gatherDuration;
            float u = s * s * (3f - 2f * s);
            for (int i = 0; i < active.Count; i++)
            {
                var g = active[i];
                if (!g) continue;

                Vector2 p = Vector2.Lerp((Vector2)starts[i], center2D, u);

                if (endRadius > 0f)
                {
                    float seed = (i * 0.6180339887f) % 1f;
                    float ang = seed * Mathf.PI * 2f;
                    Vector2 jitter = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (endRadius * (1f - u));
                    p += jitter;
                }

                g.transform.position = new Vector3(p.x, p.y, g.transform.position.z);
            }
            t += Time.deltaTime;
            yield return null;
        }

        // 4) 스냅 + 입자 비활성화 + 물리 복구
        foreach (var g in active)
        {
            if (!g) continue;
            g.transform.position = new Vector3(center2D.x, center2D.y, g.transform.position.z);
            g.SetActive(false);
        }
        foreach (var pair in savedSim)
            if (pair.rb) pair.rb.simulated = pair.was;

        // 5) 고체를 '방금 계산한' 센터로 복귀 (Z는 보존)
        RestoreSolid(new Vector3(center2D.x, center2D.y, transform.position.z));

        busy = false;
    }

    void RestoreSolid(Vector3 pos)
    {
        transform.position = pos;
        if (renderers != null) foreach (var r in renderers) if (r) r.enabled = true;
        if (rb) rb.simulated = true;
        if (col) col.enabled = true;
    }
}




