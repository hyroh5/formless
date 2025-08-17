// SolidToLiquidMorph2D.cs
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidToLiquidMorph2D : MonoBehaviour
{
    [Header("���� �̸� ��ġ�� ��ü ���ڵ�(������ ������ ���� ���)")]
    public List<GameObject> preplacedParticles = new List<GameObject>();

    [Header("������ ������(���� ���� ���/�ڱ� ��ġ)")]
    public Transform designCenter;

    [Header("������ ���� ���(�̹�ġ �� ���)")]
    public GameObject liquidParticlePrefab;
    [Min(1)] public int particleCount = 50;
    public float spawnRadius = 0.35f;

    [Header("��ȯ �� �ʱ� ��")]
    public float initialForce = 2f;      // 0�̸� ���� ����
    public bool inheritVelocity = true;  // ���� �ӵ� ���

    [Header("Ű ����")]
    public KeyCode morphKey = KeyCode.M;

    Rigidbody2D rb;
    Collider2D col;

    // �̸� ��ġ�� ���� ��� ������
    readonly List<Vector2> offsets = new List<Vector2>();

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        CapturePattern();  // �� ��ġ ���� ����
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

        // �ڱ� �浹���ð� ��Ȱ��
        foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.enabled = false;
        rb.simulated = false;

        if (preplacedParticles.Count > 0)
        {
            // ��� 1: �̸� ��ġ�� ���� ���ġ/Ȱ��
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
            // ��� 2: ������ ����
            if (!liquidParticlePrefab)
            {
                Debug.LogWarning("liquidParticlePrefab�� ����ְ�, preplacedParticles�� �����ϴ�.");
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

        Destroy(gameObject); // ��ü ����
    }

    void CapturePattern()
    {
        offsets.Clear();
        if (preplacedParticles.Count == 0) return;

        // ������ ���
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

        // ��� ������ ����
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
