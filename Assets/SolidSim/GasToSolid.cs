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
    public float gatherDuration = 0.6f;
    public float endRadius = 0.02f;

    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;
    bool busy;

    // 확정 입자 목록(부모 바뀌어도 유지)
    readonly List<GameObject> particles = new List<GameObject>();

    // 참조
    SolidToGasConverter stg;
    SolidMovement2D movement;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        renderers = GetComponentsInChildren<Renderer>(true);
        movement = GetComponent<SolidMovement2D>();
        stg = GetComponent<SolidToGasConverter>();

        ResolveParticlesOnce();
    }

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

    // 부모/개별 섞여도 Rigidbody2D 가진 실제 입자만 캐싱
    void ResolveParticlesOnce()
    {
        particles.Clear();
        var set = new HashSet<GameObject>();

        foreach (var root in gasRootsOrParticles)
        {
            if (!root) continue;

            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs) if (r) set.Add(r.gameObject);
        }

        foreach (var g in gasRootsOrParticles)
            if (g && g.TryGetComponent<Rigidbody2D>(out _)) set.Add(g);

        particles.AddRange(set);
    }

    IEnumerator CoToSolid()
    {
        busy = true;

        // 1) 활성 입자만 취합해 무게중심
        var active = new List<GameObject>(particles.Count);
        Vector2 sum = Vector2.zero;
        foreach (var g in particles)
        {
            if (g && g.activeInHierarchy)
            {
                active.Add(g);
                sum += (Vector2)g.transform.position;
            }
        }

        // 한 개도 활성 없으면 고체만 복구 + 가스 전부 OFF 보장
        if (active.Count == 0)
        {
            RestoreSolid(transform.position);
            ForceOffAllGasHierarchies();
            busy = false;
            yield break;
        }

        Vector2 center2D = sum / active.Count;

        // 2) 모으는 동안 물리 잠깐 정지
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

        // 3) 스무스하게 무게중심으로
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

        // 4) 스냅 + 가스 전체 OFF(트리) + 물리 복구
        foreach (var g in active)
        {
            if (!g) continue;
            g.transform.position = new Vector3(center2D.x, center2D.y, g.transform.position.z);
        }

        // C 눌렀으면, 어떤 구조든 가스는 "전부" 꺼져야 함
        ForceOffAllGasHierarchies();

        foreach (var pair in savedSim)
            if (pair.rb) pair.rb.simulated = pair.was;

        // 5) 고체 복구
        RestoreSolid(new Vector3(center2D.x, center2D.y, transform.position.z));

        busy = false;
    }

    void RestoreSolid(Vector3 pos)
    {
        transform.position = pos;

        // 외형 복구
        if (renderers != null) foreach (var r in renderers) if (r) r.enabled = true;

        // 물리/충돌 복구
        if (rb) rb.simulated = true;
        if (col) col.enabled = true;

        // 컨트롤/물리 잠금 복구
        if (movement)
        {
            movement.EnableControls(true);
            movement.FreezePhysics(false);
        }
    }

    // 씬 내 모든 가스 루트/입자 트리를 강제 OFF
    void ForceOffAllGasHierarchies()
    {
        // 1) 이 스크립트가 들고 있는 목록 기반 OFF
        foreach (var root in gasRootsOrParticles)
            SetHierarchyActive(root, false);

        // 2) STG가 들고 있는 목록 기반 OFF(중복되어도 상관없음)
        if (stg && stg.HasAnyGas())
        {
            foreach (var root in stg.GetGasRootsOrParticlesSafe())
                SetHierarchyActive(root, false);
        }
    }

    // 부모/자식 포함 전체 토글
    static void SetHierarchyActive(GameObject root, bool on)
    {
        if (!root) return;
        var all = root.GetComponentsInChildren<Transform>(true); // 비활성 포함
        foreach (var tr in all) tr.gameObject.SetActive(on);
    }
}





