using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VerticalFollowCamera2D : MonoBehaviour
{
    // ������������������ �ɼ� ������������������
    [Header("���ü�/���� ����")]
    [SerializeField] bool treatTargetUnavailableIfRendererHidden = true;
    [SerializeField] bool treatTargetUnavailableIfPhysicsOff = true;

    public enum FocusMode { TargetOnly, ParticlesOnly, Auto }

    [Header("���� ��Ŀ�� �ɼ�")]
    [Range(0f, 1f)] public float centerBias = 0.5f;   // 0=�ش���, 1=�߽�, 0.5=�߰�
    [Range(0f, 1f)] public float extentSmoothFactor = 0.5f; // 0=���, 1=�ſ� ����

    [Header("���(��ü)")]
    public Transform target;

    [Header("���� ��Ʈ(��ü/��ü) - ��Ʈ �Ǵ� ���� ��ƼŬ")]
    public List<GameObject> liquidRootsOrParticles = new();
    public List<GameObject> gasRootsOrParticles = new();

    [Header("�±� ��� ����(�ɼ�)")]
    public bool includeTagSearch = false;
    public string liquidTag = "Liquid";
    public string gasTag = "Gas";

    [Header("��Ŀ�� ���")]
    public FocusMode focus = FocusMode.Auto;

    [Header("����(Ÿ������� ��ȯ)")]
    public float switchBlendTime = 0.25f;   // �ҽ� ��ȯ �� Y ���� �ð�
    public float sourceStickTime = 0.15f;   // ����ȯ ����

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
    public float zoomPad = 1.2f;
    public float zoomLerpSpeed = 3f;

    [Header("���� ���� �ɼ�")]
    public int minParticleSamples = 3;  // �̺��� ������ ���� ��Ŀ�� ����
    public bool weightByMass = true;     // ���� ���� �߽�

    [Header("�ҽ� Ȧ��(��ȯ �׷��̽�)")]
    [Tooltip("���� ��ȯ ������ �ҽ��� ��� ������� ������ ��ȿ���� �����ϴ� �ð�")]
    public float missingHoldTime = 0.35f;

    // ������������������ ���� ĳ�� ������������������
    Renderer[] targetRenderers;
    Rigidbody2D targetRb;
    Collider2D targetCol;

    Camera cam;
    float velY;
    float lastFocusY;
    float lastExtent;  // �����ܿ� halfHeight�� ������ ĳ��

    enum Source { None, Target, Particles }
    Source currSource = Source.None;
    Source lastSource = Source.None;
    float sourceChangedAt = -999f;

    // �ֱ� ��ȿ ��(Ȧ���)
    float lastValidTargetY, lastValidParticlesY, lastValidParticlesHalf;
    float targetSeenAt = -999f;
    float particlesSeenAt = -999f;

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
        // 1) �̹� �������� �ĺ� ��Ŀ�� ���(Ȧ�� ����)
        bool hasTarget = TryGetTargetY(out float targetY);
        bool hasParticles = TryGetParticlesYAndExtent_WithHold(out float particlesY, out float particlesHalfHeight);

        // 2) �ҽ� ����
        Source desired = currSource;
        switch (focus)
        {
            case FocusMode.TargetOnly: desired = hasTarget ? Source.Target : Source.None; break;
            case FocusMode.ParticlesOnly: desired = hasParticles ? Source.Particles : Source.None; break;
            case FocusMode.Auto:
                desired = hasTarget ? Source.Target :
                          (hasParticles ? Source.Particles : Source.None);
                break;
        }

        if (desired != currSource && (Time.time - sourceChangedAt) >= sourceStickTime)
        {
            lastSource = currSource;
            currSource = desired;
            sourceChangedAt = Time.time;
        }

        // 3) ��ǥ Y(���� ��ü ����)
        float SafeTargetY() => hasTarget ? targetY : (targetSeenAt > 0 ? lastValidTargetY : lastFocusY);
        float SafeParticlesY() => hasParticles ? particlesY : (particlesSeenAt > 0 ? lastValidParticlesY : lastFocusY);

        float rawFocusY =
            (currSource == Source.Target && hasTarget) ? targetY :
            (currSource == Source.Particles && hasParticles) ? particlesY :
            lastFocusY;

        // 4) �ҽ� ��ȯ ����(����ȿ �ҽ��� ĳ�÷� ��ü)
        float blendedY = rawFocusY;
        if (switchBlendTime > 0f && lastSource != Source.None && currSource != lastSource)
        {
            float t = Mathf.Clamp01((Time.time - sourceChangedAt) / Mathf.Max(0.0001f, switchBlendTime));
            float fromY = (lastSource == Source.Target) ? SafeTargetY() : SafeParticlesY();
            float toY = (currSource == Source.Target) ? SafeTargetY() : SafeParticlesY();
            blendedY = Mathf.Lerp(fromY, toY, t);
        }

        // 5) ������ �� ������ �̵�
        float targetYFinal = blendedY + verticalOffset;
        float currY = transform.position.y;
        if (Mathf.Abs(targetYFinal - currY) < deadZone) targetYFinal = currY;

        float newY = Mathf.SmoothDamp(currY, targetYFinal, ref velY, smoothTime, maxFollowSpeed);
        if (clampY) newY = Mathf.Clamp(newY, minY, maxY);

        float newX = lockX ? fixedX : transform.position.x;
        transform.position = new Vector3(newX, newY, transform.position.z);
        lastFocusY = newY;

        // 6) ������(���� �ҽ��� ����, Ȧ�� ����)
        if (useAutoZoom && cam)
        {
            float targetSize = cam.orthographicSize;
            if (currSource == Source.Particles &&
                TryGetParticlesYAndExtent_WithHold(out _, out float h))
            {
                float need = h * zoomPad;
                float desiredValue = Mathf.Clamp(need, minOrthoSize, maxOrthoSize);
                targetSize = Mathf.Lerp(cam.orthographicSize, desiredValue, Time.deltaTime * zoomLerpSpeed);
            }
            else
            {
                targetSize = Mathf.Lerp(cam.orthographicSize, minOrthoSize, Time.deltaTime * zoomLerpSpeed);
            }
            cam.orthographicSize = targetSize;
        }
    }

    // ������������������ ��Ŀ�� ��� ������������������

    // Ÿ�� Y(Ȧ�� ����)
    bool TryGetTargetY(out float y)
    {
        y = 0f;

        bool visibleOk = true;
        bool physicsOk = true;

        if (!target || !target.gameObject.activeInHierarchy) visibleOk = false;

        if (visibleOk && treatTargetUnavailableIfRendererHidden && targetRenderers != null && targetRenderers.Length > 0)
        {
            bool anyVisible = false;
            foreach (var r in targetRenderers) { if (r && r.enabled) { anyVisible = true; break; } }
            if (!anyVisible) visibleOk = false;
        }

        if (visibleOk && treatTargetUnavailableIfPhysicsOff)
        {
            if (targetRb && !targetRb.simulated) physicsOk = false;
            if (targetCol && !targetCol.enabled) physicsOk = false;
        }

        if (visibleOk && physicsOk)
        {
            y = target.position.y;
            lastValidTargetY = y;
            targetSeenAt = Time.time;
            return true;
        }

        // �ֱ� �� Ȧ��
        if (Time.time - targetSeenAt <= missingHoldTime)
        {
            y = lastValidTargetY;
            return true;
        }
        return false;
    }

    // ���� ��Ŀ��(���)
    bool TryGetParticlesYAndExtent(out float focusY, out float halfHeight)
    {
        focusY = 0f; halfHeight = 0f;

        // 1) ���� ����
        var liq = new List<Transform>(64);
        var gas = new List<Transform>(64);
        AccumulateFromRoots(liquidRootsOrParticles, liq);
        AccumulateFromRoots(gasRootsOrParticles, gas);

        if (includeTagSearch)
        {
            if (!string.IsNullOrEmpty(liquidTag)) AccumulateFromTag(liquidTag, liq);
            if (!string.IsNullOrEmpty(gasTag)) AccumulateFromTag(gasTag, gas);
        }

        // 2) Ȱ�� ��Ʈ ����(��ü �켱)
        List<Transform> use = null;
        bool usingLiquid = false;
        if (liq.Count >= minParticleSamples) { use = liq; usingLiquid = true; }
        else if (gas.Count >= minParticleSamples) { use = gas; usingLiquid = false; }
        else return false;

        // 3) min/max, �������� �߽�
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        double sumW = 0.0, sumYw = 0.0;

        foreach (var t in use)
        {
            if (!t) continue;
            float y = t.position.y;

            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            float w = 1f;
            if (weightByMass)
            {
                var rb2d = t.GetComponent<Rigidbody2D>();
                if (rb2d) w = Mathf.Max(0.0001f, rb2d.mass);
            }
            sumW += w;
            sumYw += y * w;
        }
        if (sumW <= 0.0) return false;

        float centroidY = (float)(sumYw / sumW);
        float extremeY = usingLiquid ? minY : maxY;

        // 4) ��Ŀ�� Y: �ش������߽� ���� �߰���
        focusY = Mathf.Lerp(extremeY, centroidY, Mathf.Clamp01(centerBias));

        // 5) �����ܿ� ���� �ݳ���(�� ���� ���� �����̹� ����)
        float halfBySpread = Mathf.Max(0f, (maxY - minY) * 0.5f);
        float halfByTwoPoints = Mathf.Abs(extremeY - centroidY) * 0.5f;
        float halfRaw = Mathf.Max(halfBySpread, halfByTwoPoints);

        // 6) ���� ������
        float lerpT = Mathf.Clamp01(1f - extentSmoothFactor); // 0=���, 1=����
        lastExtent = Mathf.Lerp(lastExtent <= 0f ? halfRaw : lastExtent, halfRaw, lerpT);
        halfHeight = lastExtent;

        // ĳ��(Ȧ���)
        lastValidParticlesY = focusY;
        lastValidParticlesHalf = halfRaw;
        particlesSeenAt = Time.time;

        return true;
    }

    // Ȧ�� ����
    bool TryGetParticlesYAndExtent_WithHold(out float focusY, out float halfHeight)
    {
        if (TryGetParticlesYAndExtent(out focusY, out halfHeight))
            return true;

        if (Time.time - particlesSeenAt <= missingHoldTime)
        {
            focusY = lastValidParticlesY;
            // halfHeight�� �� �������� ���� "�������� ��"�� ��� ���
            halfHeight = (lastExtent > 0f) ? lastExtent : lastValidParticlesHalf;
            return true;
        }

        focusY = 0f; halfHeight = 0f;
        return false;
    }

    // ������������������ ���� ��ƿ ������������������
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
