// SolidToLiquidMorph2D.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquidMorph2D : MonoBehaviour
{
    [Header("씬에 미리 배치한 액체 입자들(없으면 프리팹 생성 사용)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("디자인 기준점(비우면 입자 평균/자기 위치)")]
    public Transform designCenter;

    [Header("프리팹 생성 경로(미배치 시 사용)")]
    public GameObject liquidParticlePrefab;
    [Min(1)] public int particleCount = 50;
    public float spawnRadius = 0.35f;

    [Header("변환 시 초기 힘")]
    public float initialForce = 2f;      // 0이면 퍼짐 없음
    public bool inheritVelocity = true;  // 원의 속도 상속

    [Header("키 설정")]
    public KeyCode morphKey = KeyCode.M;

    Rigidbody2D rb;
    Collider2D col;

    // 미리 배치한 입자 상대 오프셋
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CapturePattern();  // 씬 배치 패턴 저장
    }

    void Start()
    {
        foreach (var g in preplacedParticles)
            if (g) g.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(morphKey))
            TransformToLiquid();
    }

    public void TransformToLiquid()
    {
        Vector2 center = col ? (Vector2)col.bounds.center : (Vector2)transform.position;
        Vector2 inheritVel = inheritVelocity ? rb.velocity : Vector2.zero;

        // 자기 충돌·시각 비활성
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

        Destroy(gameObject); // 고체 제거
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

        // 상대 오프셋 저장
        foreach (var g in preplacedParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    static bool HasInactiveAncestor(Transform t)
    {
        for (Transform a = t.parent; a != null; a = a.parent)
            if (!a.gameObject.activeSelf) return true;
        return false;
    }
}
