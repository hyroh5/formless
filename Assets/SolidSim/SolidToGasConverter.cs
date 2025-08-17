using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToGasConverter : MonoBehaviour
{
    [Header("씬에 미리 배치한 기체 입자들(부모 필요 없음)")]
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
    SolidMovement2D movement;           // 이동 스크립트 분리 참조
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        movement = GetComponent<SolidMovement2D>();
        renderers = GetComponentsInChildren<Renderer>(true);

        CapturePattern(); // 가스 입자 패턴 기억
    }

    void Start()
    {
        // 시작 시 가스 입자 숨김
        foreach (var g in gasParticles)
            if (g) g.SetActive(false);
    }

    void Update()
    {
        if (listenHotkey && Input.GetKeyDown(convertKey))
            ConvertToGas();
    }

    // --- 외부에서 호출 가능 API ---
    public void ConvertToGas()
    {
        Vector2 spawnCenter = col ? (Vector2)col.bounds.center : (Vector2)transform.position;

        // 고체: 렌더러 숨김 + 입력/물리 차단
        if (renderers != null)
            foreach (var r in renderers)
                if (r) r.enabled = false;

        if (movement) movement.EnableControls(false);
        if (movement) movement.FreezePhysics(true);
        else
        {
            if (rb) rb.simulated = false;
            if (col) col.enabled = false;
        }

        // 가스 입자 활성화 + 배치 + 초기 속도
        for (int i = 0; i < gasParticles.Count; i++)
        {
            var g = gasParticles[i];
            if (!g) continue;

            // 조상 비활성 상태면, 이 입자만 분리(월드 루트로)
            if (HasInactiveAncestor(g.transform))
                g.transform.SetParent(null, true);

            if (!g.activeSelf) g.SetActive(true);

            Vector2 off = i < offsets.Count ? offsets[i] : Vector2.zero;
            g.transform.position = spawnCenter + off;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                Vector2 dir = off.sqrMagnitude > 1e-8f ? off.normalized : Vector2.zero;
                grb.velocity = dir * initialForce;
            }
        }
        // 파괴 안 함(복구 가능성 열어둠)
    }

    // --- 초기 디자인 패턴(상대 좌표) 기억 ---
    void CapturePattern()
    {
        offsets.Clear();
        if (gasParticles.Count == 0) return;

        Vector2 center;
        if (designCenter) center = designCenter.position;
        else
        {
            Vector2 sum = Vector2.zero; int n = 0;
            foreach (var g in gasParticles)
            {
                if (!g) continue;
                sum += (Vector2)g.transform.position; n++;
            }
            center = n > 0 ? sum / n : (Vector2)transform.position;
        }

        foreach (var g in gasParticles)
            offsets.Add(g ? ((Vector2)g.transform.position - center) : Vector2.zero);
    }

    static bool HasInactiveAncestor(Transform t)
    {
        for (Transform a = t.parent; a != null; a = a.parent)
            if (!a.gameObject.activeSelf) return true;
        return false;
    }
}

