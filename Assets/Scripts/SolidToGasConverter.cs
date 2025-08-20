using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToGasConverter : MonoBehaviour
{
    [Header("가스 루트(부모 폴더) 또는 개별 입자들")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("원 중심")]
    public Transform designCenter;

    [Header("변환 설정")]
    public float initialForce = 2f;
    public float upwardBias   = 1.5f;    // 위로 뜨는 힘 추가
    public bool listenHotkey = true;
    public KeyCode convertKey = KeyCode.X;

    Rigidbody2D rb;
    Collider2D col;
    Renderer[] renderers;
    readonly List<Vector2> offsets = new List<Vector2>();

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
        Vector2 spawnCenter = col ? (Vector2)col.bounds.center : (Vector2)transform.position;

        if (renderers != null) foreach (var r in renderers) if (r) r.enabled = false;
        if (rb)  rb.simulated = false;
        if (col) col.enabled = false;

        var allGasLeafs = ResolveAllLeafs();
        foreach (var root in gasParticles)
            SetHierarchyActive(root, true);

        for (int i = 0; i < allGasLeafs.Count; i++)
        {
            var g = allGasLeafs[i];
            if (!g) continue;

            var rends = g.GetComponentsInChildren<Renderer>(true);
            foreach (var rr in rends) if (rr) rr.enabled = true;

            var pss = g.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in pss) if (ps && !ps.isPlaying) ps.Play();

            var srs = g.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in srs)
                if (sr) { var c = sr.color; c.a = 1f; sr.color = c; }

            Vector2 off = i < offsets.Count ? offsets[i] : Vector2.zero;
            g.transform.position = spawnCenter + off;

            var grb = g.GetComponent<Rigidbody2D>();
            if (grb)
            {
                Vector2 dir = (off.sqrMagnitude > 1e-8f) ? off.normalized : Random.insideUnitCircle.normalized;
                dir += Vector2.up * upwardBias; // 위로 힘 추가
                grb.velocity = dir.normalized * initialForce;
            }
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

    List<GameObject> ResolveAllLeafs()
    {
        var list = new List<GameObject>();
        var set = new HashSet<GameObject>();

        foreach (var root in gasParticles)
        {
            if (!root) continue;
            if (root.TryGetComponent<Rigidbody2D>(out _)) set.Add(root);
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs) if (r) set.Add(r.gameObject);
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

