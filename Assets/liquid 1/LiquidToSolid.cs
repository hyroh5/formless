// LiquidToSolidRemorph2D.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LiquidToSolid : MonoBehaviour
{
    [Header("��ü ���� �θ�(����) �Ǵ� ���� ���ڵ� (���� �־ ��)")]
    public List<GameObject> liquidRootsOrParticles = new List<GameObject>();

    [Header("�±� ��� ���� ���� (�ɼ�)")]
    public bool includeTagSearch = true;
    public string liquidTag = "Liquid";   // ��ü ������/�ν��Ͻ��� �� �±׸� �޾Ƶθ� �ڵ� ������

    [Header("��ü ������ (�ݵ�� ����)")]
    public GameObject solidPrefab;

    [Header("Ű / ���̱� ����")]
    public KeyCode toSolidKey = KeyCode.C;
    public float gatherDuration = 0.6f;   // ������ ���� �ð�
    public float endRadius = 0.02f;       // ���� �ܶ���(0�̸� ���� �� ��)

    [Header("���� ��ġ/�ӵ� �ɼ�")]
    public bool alignToGround = true;     // �ٴڿ� ���� ��¦ ��� ��ġ
    public LayerMask groundLayer;
    public float solidRadius = 0.5f;      // �� ��ü ������(�ٴڿ��� �̸�ŭ ���� �ø�)
    public bool inheritAverageVelocity = true; // ���� ��ռӵ��� ��ü �ʱ�ӵ���

    [Header("���� ���")]
    public bool destroyParticlesOnSolidify = false; // true�� ��ü �� ���� Destroy, false�� ��Ȱ��

    bool busy;

    // ���� ������ �����硱 ��Ƽ�� ���ڸ� ������ �Լ�(�θ�/����/�±� ��� ����)
    void ResolveParticlesRuntime(List<GameObject> outList)
    {
        outList.Clear();
        var set = new HashSet<GameObject>();

        // �θ�/�������� ����
        foreach (var root in liquidRootsOrParticles)
        {
            if (!root) continue;

            // �ڱ� �ڽ��� ���ڸ� �߰�
            if (root.TryGetComponent<Rigidbody2D>(out _))
                set.Add(root);

            // �ڽ� �� Rigidbody2D�� ���� ��Ƽ��/��Ȱ�� ����
            var rbs = root.GetComponentsInChildren<Rigidbody2D>(true);
            foreach (var r in rbs)
                if (r) set.Add(r.gameObject);
        }

        // �±� ��� ����(�ɼ�)
        if (includeTagSearch && !string.IsNullOrEmpty(liquidTag))
        {
            var gos = GameObject.FindGameObjectsWithTag(liquidTag);
            foreach (var go in gos)
                if (go && go.TryGetComponent<Rigidbody2D>(out _))
                    set.Add(go);
        }

        // ���� ���� (���� ������ Ȱ���� �ֵ鸸 ������ ���)
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

        // 1) ���� Ȱ�� ��ü ���� ���� ����
        var active = new List<GameObject>(64);
        ResolveParticlesRuntime(active);

        if (active.Count == 0)
        {
            Debug.LogWarning("[LiquidToSolid] Ȱ�� ��ü ���ڸ� ã�� ���߾��.");
            busy = false;
            yield break;
        }

        // 2) ��Ʈ���̵�/��ռӵ� ���
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

        // �ٴ� ���� �ɼ�: ���� �Ʒ��� ����ĳ��Ʈ
        if (alignToGround)
        {
            var hit = Physics2D.Raycast(center + Vector2.up * 0.2f, Vector2.down, 5f, groundLayer);
            if (hit.collider)
                center = hit.point + hit.normal.normalized * solidRadius;
        }

        // 3) ������ ���� ���� ��� �� + ���� ��ġ ����
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
                // �ùķ��̼��� ���� �浹���� ��ġ�� ���� ����
                rb2d.simulated = false;
            }
        }

        // 4) �ε巴�� �����߽����� ������ (smoothstep)
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
                    // ���ڸ��� �ٸ� �������� �̼� ��鸲
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

        // 5) ���� + ���� ����(��Ȱ�� �Ǵ� �ı�) + ���� ����
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

        // 6) ��ü ����
        if (!solidPrefab)
        {
            Debug.LogError("[LiquidToSolidRemorph2D] solidPrefab�� ����ֽ��ϴ�.");
        }
        else
        {
            var solid = Instantiate(solidPrefab, center, Quaternion.identity);

            // ��� �ӵ� ���(����)
            if (inheritAverageVelocity)
            {
                var srb = solid.GetComponent<Rigidbody2D>();
                if (srb) srb.velocity = avgVel;
            }
        }

        busy = false;
    }
}
