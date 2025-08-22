using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VerticalFollowCamera2D : MonoBehaviour
{
    // ───────── 옵션 ─────────
    [Header("가시성/물리 조건")]
    [SerializeField] bool treatTargetUnavailableIfRendererHidden = true;
    [SerializeField] bool treatTargetUnavailableIfPhysicsOff = true;

    public enum FocusMode { TargetOnly, ParticlesOnly, Auto }

    [Header("입자 포커싱 옵션")]
    [Range(0f, 1f)] public float centerBias = 0.5f;   // 0=극단점, 1=중심, 0.5=중간
    [Range(0f, 1f)] public float extentSmoothFactor = 0.5f; // 0=즉시, 1=매우 느림

    [Header("대상(고체)")]
    public Transform target;

    [Header("입자 세트(액체/기체) - 루트 또는 개별 파티클")]
    public List<GameObject> liquidRootsOrParticles = new();
    public List<GameObject> gasRootsOrParticles = new();

    [Header("태그 기반 수집(옵션)")]
    public bool includeTagSearch = false;
    public string liquidTag = "Liquid";
    public string gasTag = "Gas";

    [Header("포커스 모드")]
    public FocusMode focus = FocusMode.Auto;

    [Header("블렌드(타깃↔입자 전환)")]
    public float switchBlendTime = 0.25f;   // 소스 전환 시 Y 보간 시간
    public float sourceStickTime = 0.15f;   // 재전환 억제

    [Header("Y 이동 스무딩")]
    public float smoothTime = 0.25f;
    public float maxFollowSpeed = 50f;
    public float verticalOffset = 0f;
    public float deadZone = 0.1f;

    [Header("수직 범위 클램프(옵션)")]
    public bool clampY = false;
    public float minY = -100f;
    public float maxY = 100f;

    [Header("수평 고정(풀샷)")]
    public bool lockX = true;
    public float fixedX = 0f;

    [Header("오토 줌(입자 프레이밍)")]
    public bool useAutoZoom = true;
    public float minOrthoSize = 5f;
    public float maxOrthoSize = 12f;
    public float zoomPad = 1.2f;
    public float zoomLerpSpeed = 3f;

    [Header("입자 샘플 옵션")]
    public int minParticleSamples = 3;  // 이보다 적으면 입자 포커스 무시
    public bool weightByMass = true;     // 질량 가중 중심

    [Header("소스 홀드(전환 그레이스)")]
    [Tooltip("상태 전환 등으로 소스가 잠깐 사라져도 마지막 유효값을 유지하는 시간")]
    public float missingHoldTime = 0.35f;

    // ───────── 내부 캐시 ─────────
    Renderer[] targetRenderers;
    Rigidbody2D targetRb;
    Collider2D targetCol;

    Camera cam;
    float velY;
    float lastFocusY;
    float lastExtent;  // 오토줌용 halfHeight의 스무딩 캐시

    enum Source { None, Target, Particles }
    Source currSource = Source.None;
    Source lastSource = Source.None;
    float sourceChangedAt = -999f;

    // 최근 유효 값(홀드용)
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
        // 1) 이번 프레임의 후보 포커스 계산(홀드 적용)
        bool hasTarget = TryGetTargetY(out float targetY);
        bool hasParticles = TryGetParticlesYAndExtent_WithHold(out float particlesY, out float particlesHalfHeight);

        // 2) 소스 선택
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

        // 3) 목표 Y(안전 대체 포함)
        float SafeTargetY() => hasTarget ? targetY : (targetSeenAt > 0 ? lastValidTargetY : lastFocusY);
        float SafeParticlesY() => hasParticles ? particlesY : (particlesSeenAt > 0 ? lastValidParticlesY : lastFocusY);

        float rawFocusY =
            (currSource == Source.Target && hasTarget) ? targetY :
            (currSource == Source.Particles && hasParticles) ? particlesY :
            lastFocusY;

        // 4) 소스 전환 블렌딩(미유효 소스는 캐시로 대체)
        float blendedY = rawFocusY;
        if (switchBlendTime > 0f && lastSource != Source.None && currSource != lastSource)
        {
            float t = Mathf.Clamp01((Time.time - sourceChangedAt) / Mathf.Max(0.0001f, switchBlendTime));
            float fromY = (lastSource == Source.Target) ? SafeTargetY() : SafeParticlesY();
            float toY = (currSource == Source.Target) ? SafeTargetY() : SafeParticlesY();
            blendedY = Mathf.Lerp(fromY, toY, t);
        }

        // 5) 데드존 및 스무딩 이동
        float targetYFinal = blendedY + verticalOffset;
        float currY = transform.position.y;
        if (Mathf.Abs(targetYFinal - currY) < deadZone) targetYFinal = currY;

        float newY = Mathf.SmoothDamp(currY, targetYFinal, ref velY, smoothTime, maxFollowSpeed);
        if (clampY) newY = Mathf.Clamp(newY, minY, maxY);

        float newX = lockX ? fixedX : transform.position.x;
        transform.position = new Vector3(newX, newY, transform.position.z);
        lastFocusY = newY;

        // 6) 오토줌(입자 소스일 때만, 홀드 포함)
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

    // ───────── 포커스 계산 ─────────

    // 타깃 Y(홀드 포함)
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

        // 최근 값 홀드
        if (Time.time - targetSeenAt <= missingHoldTime)
        {
            y = lastValidTargetY;
            return true;
        }
        return false;
    }

    // 입자 포커스(계산)
    bool TryGetParticlesYAndExtent(out float focusY, out float halfHeight)
    {
        focusY = 0f; halfHeight = 0f;

        // 1) 샘플 수집
        var liq = new List<Transform>(64);
        var gas = new List<Transform>(64);
        AccumulateFromRoots(liquidRootsOrParticles, liq);
        AccumulateFromRoots(gasRootsOrParticles, gas);

        if (includeTagSearch)
        {
            if (!string.IsNullOrEmpty(liquidTag)) AccumulateFromTag(liquidTag, liq);
            if (!string.IsNullOrEmpty(gasTag)) AccumulateFromTag(gasTag, gas);
        }

        // 2) 활성 세트 선택(액체 우선)
        List<Transform> use = null;
        bool usingLiquid = false;
        if (liq.Count >= minParticleSamples) { use = liq; usingLiquid = true; }
        else if (gas.Count >= minParticleSamples) { use = gas; usingLiquid = false; }
        else return false;

        // 3) min/max, 질량가중 중심
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

        // 4) 포커스 Y: 극단점↔중심 가중 중간값
        focusY = Mathf.Lerp(extremeY, centroidY, Mathf.Clamp01(centerBias));

        // 5) 오토줌용 세로 반높이(두 지점 동시 프레이밍 보장)
        float halfBySpread = Mathf.Max(0f, (maxY - minY) * 0.5f);
        float halfByTwoPoints = Mathf.Abs(extremeY - centroidY) * 0.5f;
        float halfRaw = Mathf.Max(halfBySpread, halfByTwoPoints);

        // 6) 범위 스무딩
        float lerpT = Mathf.Clamp01(1f - extentSmoothFactor); // 0=즉시, 1=느림
        lastExtent = Mathf.Lerp(lastExtent <= 0f ? halfRaw : lastExtent, halfRaw, lerpT);
        halfHeight = lastExtent;

        // 캐시(홀드용)
        lastValidParticlesY = focusY;
        lastValidParticlesHalf = halfRaw;
        particlesSeenAt = Time.time;

        return true;
    }

    // 홀드 래퍼
    bool TryGetParticlesYAndExtent_WithHold(out float focusY, out float halfHeight)
    {
        if (TryGetParticlesYAndExtent(out focusY, out halfHeight))
            return true;

        if (Time.time - particlesSeenAt <= missingHoldTime)
        {
            focusY = lastValidParticlesY;
            // halfHeight는 줌 안정성을 위해 "스무딩된 값"을 계속 사용
            halfHeight = (lastExtent > 0f) ? lastExtent : lastValidParticlesHalf;
            return true;
        }

        focusY = 0f; halfHeight = 0f;
        return false;
    }

    // ───────── 수집 유틸 ─────────
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
