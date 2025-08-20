using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VerticalFollowCamera2D : MonoBehaviour
{
    // 1) Ŭ���� �ʵ忡 �߰� (��ȣ���� �°� on/off ����)
    [SerializeField] bool treatTargetUnavailableIfRendererHidden = true;
    [SerializeField] bool treatTargetUnavailableIfPhysicsOff = true;

    // ĳ�ÿ�
    Renderer[] targetRenderers;
    Rigidbody2D targetRb;
    Collider2D targetCol;

    public enum FocusMode { TargetOnly, ParticlesOnly, Auto }

    [Header("���(��ü)")]
    public Transform target;

    [Header("���� ��Ʈ(��ü/��ü) - ��Ʈ(�θ�) �Ǵ� ���� ��ƼŬ")]
    public List<GameObject> liquidRootsOrParticles = new();
    public List<GameObject> gasRootsOrParticles = new();

    [Header("�±� ��� ����(�ɼ�)")]
    public bool includeTagSearch = false;
    public string liquidTag = "Liquid";
    public string gasTag = "Gas";

    [Header("��Ŀ�� ���")]
    public FocusMode focus = FocusMode.Auto;

    [Header("����(Ÿ������� ��ȯ)")]
    public float switchBlendTime = 0.25f;  // ��Ŀ�� �ҽ� �ٲ� �� Y�� �� �ð����� ����
    public float sourceStickTime = 0.15f;  // �ҽ� �ٲ� ���� ����ȯ ����(�����׸��ý�)

    [Header("Y �̵� ������")]
    public float smoothTime = 0.25f;
    public float maxFollowSpeed = 50f;
    public float verticalOffset = 0f;
    public float deadZone = 0.1f;

    [Header("���� ���� Ŭ����(�ɼ�)")]
    public bool clampY = false;
    public float minY = -100f;
    public float maxY = 100f;

    [Header("���� ����(Ǯ��)")]
    public bool lockX = true;
    public float fixedX = 0f;

    [Header("���� ��(���� �����̹�)")]
    public bool useAutoZoom = true;
    public float minOrthoSize = 5f;
    public float maxOrthoSize = 12f;
    public float zoomPad = 1.2f;           // ���� ������ ���� �� ����
    public float zoomLerpSpeed = 3f;

    [Header("���� ���� �ɼ�")]
    public int minParticleSamples = 3;     // �̺��� ������ ���� ��Ŀ�� ����
    public bool weightByMass = true;       // �����߽� ��꿡 mass ����
    public float extentSmoothing = 0.2f;   // ������ �� ���� ������(0~1, 0=���)

    Camera cam;

    // ����
    float velY;
    float lastFocusY;
    float lastExtent;          // ���� �������� ���� ����(����-������)
    enum Source { None, Target, Particles }
    Source currSource = Source.None;
    Source lastSource = Source.None;
    float sourceChangedAt = -999f;

    // Awake() ���κп� ĳ�� �߰�
    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam) cam.orthographic = true;
        lastFocusY = transform.position.y;

        if (target)
        {
            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            targetRb = target.GetComponent<Rigidbody2D>();
            targetCol = target.GetComponent<Collider2D>();
        }
    }

    void LateUpdate()
    {
        // 1) �̹� �������� �ĺ� ��Ŀ��(Y�� ���ι���) ���
        bool hasTarget = TryGetTargetY(out float targetY);
        bool hasParticles = TryGetParticlesYAndExtent(out float particlesY, out float particlesHalfHeight);

        // 2) � �ҽ��� ���� ���� (Auto�� ���� ��ȯ)
        Source desired = currSource;
        switch (focus)
        {
            case FocusMode.TargetOnly:
                desired = hasTarget ? Source.Target : Source.None;
                break;
            case FocusMode.ParticlesOnly:
                desired = hasParticles ? Source.Particles : Source.None;
                break;
            case FocusMode.Auto:
                // Ÿ�� ��������� �켱, ������ ����
                desired = hasTarget ? Source.Target : (hasParticles ? Source.Particles : Source.None);
                break;
        }

        // �����׸��ý�: ���� �ð� �ȿ��� �ҽ� �޺� ����
        if (desired != currSource && (Time.time - sourceChangedAt) >= sourceStickTime)
        {
            lastSource = currSource;
            currSource = desired;
            sourceChangedAt = Time.time;
        }

        // 3) �ҽ��� ���� ��ǥ Y
        float rawFocusY;
        if (currSource == Source.Target && hasTarget) rawFocusY = targetY;
        else if (currSource == Source.Particles && hasParticles) rawFocusY = particlesY;
        else rawFocusY = lastFocusY;

        // 4) �ҽ� ��ȯ ���̸� Y�� �ε巴�� ����
        float blendedY = rawFocusY;
        if (switchBlendTime > 0f && lastSource != Source.None && currSource != lastSource)
        {
            float t = Mathf.InverseLerp(0f, switchBlendTime, Time.time - sourceChangedAt);
            t = Mathf.Clamp01(t);
            float fromY = lastSource == Source.Target ? targetY : particlesY;
            float toY = currSource == Source.Target ? targetY : particlesY;
            blendedY = Mathf.Lerp(fromY, toY, t);
        }

        // 5) ������ & ������
        float targetYFinal = blendedY + verticalOffset;
        float currY = transform.position.y;
        if (Mathf.Abs(targetYFinal - currY) < deadZone)
            targetYFinal = currY;

        float newY = Mathf.SmoothDamp(currY, targetYFinal, ref velY, smoothTime, maxFollowSpeed);

        if (clampY) newY = Mathf.Clamp(newY, minY, maxY);

        float newX = lockX ? fixedX : transform.position.x;
        transform.position = new Vector3(newX, newY, transform.position.z);

        lastFocusY = newY;

        // 6) ������: ���� ������ ���� �����̹�(������ ����)
        if (useAutoZoom && cam)
        {
            float targetSize = cam.orthographicSize;

            if (currSource == Source.Particles && hasParticles)
            {
                // ������ �е� ����
                float desiredValue = Mathf.Clamp(particlesHalfHeight * zoomPad, minOrthoSize, maxOrthoSize);

                // ������ �� ���� ������(Ƣ�� Ȯ��/��� ����)
                lastExtent = Mathf.Lerp(lastExtent, desiredValue, 1f - Mathf.Exp(-extentSmoothing * Mathf.Max(0.0001f, Time.deltaTime)));

                targetSize = lastExtent;
            }
            else
            {
                // Ÿ�� ���/���� ���� õõ�� �ּ� ������ ȸ��
                targetSize = Mathf.Lerp(cam.orthographicSize, minOrthoSize, Time.deltaTime * zoomLerpSpeed);
            }

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * zoomLerpSpeed);
        }
    }

    // ------- ��Ŀ�� �ҽ� ��� -------

    // 2) �� �Լ� ��ü ��ü
    bool TryGetTargetY(out float y)
    {
        y = 0f;
        if (!target || !target.gameObject.activeInHierarchy) return false;

        // (A) �������� ��� ���� ������ '�� ���̴� ��'���� ����
        if (treatTargetUnavailableIfRendererHidden && targetRenderers != null && targetRenderers.Length > 0)
        {
            bool anyVisible = false;
            foreach (var r in targetRenderers)
            {
                if (r && r.enabled) { anyVisible = true; break; }
            }
            if (!anyVisible) return false;
        }

        // (B) ������ ���� ������(���� ��) '���� �Ұ�'�� ����
        if (treatTargetUnavailableIfPhysicsOff)
        {
            if (targetRb && !targetRb.simulated) return false;
            if (targetCol && !targetCol.enabled) return false;
        }

        y = target.position.y;
        return true;
    }


    /// <summary>
    /// ���ڵ��� �߽� Y�� ���� �ݳ���(= (maxY-minY)/2 )�� ���.
    /// ������ �����ϸ� false.
    /// </summary>
    bool TryGetParticlesYAndExtent(out float centerY, out float halfHeight)
    {
        centerY = 0f; halfHeight = 0f;

        List<Transform> samples = CollectActiveParticleSamples();
        if (samples.Count < minParticleSamples) return false;

        // ����/���� �߽�
        float sumY = 0f;
        float sumW = 0f;
        float minPy = float.PositiveInfinity, maxPy = float.NegativeInfinity;

        foreach (var t in samples)
        {
            float w = 1f;
            if (weightByMass)
            {
                var rb2d = t.GetComponent<Rigidbody2D>();
                if (rb2d) w = Mathf.Max(0.0001f, rb2d.mass);
            }

            float py = t.position.y;
            sumY += py * w;
            sumW += w;

            if (py < minPy) minPy = py;
            if (py > maxPy) maxPy = py;
        }

        centerY = (sumW > 0f) ? (sumY / sumW) : transform.position.y;
        halfHeight = Mathf.Max((maxPy - minPy) * 0.5f, 0f);
        return true;
    }

    List<Transform> CollectActiveParticleSamples()
    {
        var outList = new List<Transform>(64);
        AccumulateFromRoots(liquidRootsOrParticles, outList);
        AccumulateFromRoots(gasRootsOrParticles, outList);

        if (includeTagSearch)
        {
            if (!string.IsNullOrEmpty(liquidTag))
                AccumulateFromTag(liquidTag, outList);
            if (!string.IsNullOrEmpty(gasTag))
                AccumulateFromTag(gasTag, outList);
        }
        return outList;
    }

    static void AccumulateFromRoots(List<GameObject> rootsOrParticles, List<Transform> outList)
    {
        if (rootsOrParticles == null) return;
        foreach (var root in rootsOrParticles)
        {
            if (!root) continue;

            if (root.activeInHierarchy && (root.GetComponent<Renderer>() || root.GetComponent<Rigidbody2D>()))
                outList.Add(root.transform);

            var trs = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in trs)
            {
                if (!t || !t.gameObject.activeInHierarchy) continue;
                if (t.GetComponent<Renderer>() || t.GetComponent<Rigidbody2D>())
                    outList.Add(t);
            }
        }
    }

    static void AccumulateFromTag(string tag, List<Transform> outList)
    {
        var gos = GameObject.FindGameObjectsWithTag(tag);
        foreach (var go in gos)
        {
            if (!go || !go.activeInHierarchy) continue;
            if (go.GetComponent<Renderer>() || go.GetComponent<Rigidbody2D>())
                outList.Add(go.transform);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (clampY)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.6f, 0.5f);
            Gizmos.DrawLine(new Vector3(-999f, minY, 0f), new Vector3(999f, minY, 0f));
            Gizmos.DrawLine(new Vector3(-999f, maxY, 0f), new Vector3(999f, maxY, 0f));
        }
    }
#endif
}
