using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Camera))]
public class VerticalFollowCamera2D : MonoBehaviour
{
    // 1) 클래스 필드에 추가 (선호도에 맞게 on/off 가능)
    [SerializeField] bool treatTargetUnavailableIfRendererHidden = true;
    [SerializeField] bool treatTargetUnavailableIfPhysicsOff = true;

    // 캐시용
    Renderer[] targetRenderers;
    Rigidbody2D targetRb;
    Collider2D targetCol;

    public enum FocusMode { TargetOnly, ParticlesOnly, Auto }

    [Header("입자 포커싱 옵션")]
    [Range(0f, 1f)] public float centerBias = 0.5f; // 0=극단점만, 1=중심만, 0.5=중간
    public float extentSmoothFactor = 0.5f;          // 0=즉시, 1=매우 느림(기존 extentSmoothing 대체용)


    [Header("대상(고체)")]
    public Transform target;

    [Header("입자 세트(액체/기체) - 루트(부모) 또는 개별 파티클")]
    public List<GameObject> liquidRootsOrParticles = new();
    public List<GameObject> gasRootsOrParticles = new();

    [Header("태그 기반 수집(옵션)")]
    public bool includeTagSearch = false;
    public string liquidTag = "Liquid";
    public string gasTag = "Gas";

    [Header("포커스 모드")]
    public FocusMode focus = FocusMode.Auto;

    [Header("블렌드(타깃↔입자 전환)")]
    public float switchBlendTime = 0.25f;  // 포커스 소스 바뀔 때 Y를 이 시간동안 섞기
    public float sourceStickTime = 0.15f;  // 소스 바뀐 직후 재전환 방지(히스테리시스)

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
    public float zoomPad = 1.2f;           // 입자 범위에 더해 줄 여유
    public float zoomLerpSpeed = 3f;

    [Header("입자 샘플 옵션")]
    public int minParticleSamples = 3;     // 이보다 적으면 입자 포커스 무시
    public bool weightByMass = true;       // 무게중심 계산에 mass 가중
    public float extentSmoothing = 0.2f;   // 프레임 간 범위 스무딩(0~1, 0=즉시)

    Camera cam;

    // 상태
    float velY;
    float lastFocusY;
    float lastExtent;          // 지난 프레임의 절반 높이(세미-스무딩)
    enum Source { None, Target, Particles }
    Source currSource = Source.None;
    Source lastSource = Source.None;
    float sourceChangedAt = -999f;

    // Awake() 끝부분에 캐싱 추가
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
        // 1) 이번 프레임의 후보 포커스(Y와 세로범위) 계산
        bool hasTarget = TryGetTargetY(out float targetY);
        bool hasParticles = TryGetParticlesYAndExtent(out float particlesY, out float particlesHalfHeight);

        // 2) 어떤 소스를 쓸지 결정 (Auto일 때만 전환)
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
                // 타깃 살아있으면 우선, 없으면 입자
                desired = hasTarget ? Source.Target : (hasParticles ? Source.Particles : Source.None);
                break;
        }

        // 히스테리시스: 일정 시간 안에는 소스 급변 방지
        if (desired != currSource && (Time.time - sourceChangedAt) >= sourceStickTime)
        {
            lastSource = currSource;
            currSource = desired;
            sourceChangedAt = Time.time;
        }

        // 3) 소스에 따른 목표 Y
        float rawFocusY;
        if (currSource == Source.Target && hasTarget) rawFocusY = targetY;
        else if (currSource == Source.Particles && hasParticles) rawFocusY = particlesY;
        else rawFocusY = lastFocusY;

        // 4) 소스 전환 중이면 Y를 부드럽게 블렌드
        float blendedY = rawFocusY;
        if (switchBlendTime > 0f && lastSource != Source.None && currSource != lastSource)
        {
            float t = Mathf.InverseLerp(0f, switchBlendTime, Time.time - sourceChangedAt);
            t = Mathf.Clamp01(t);
            float fromY = lastSource == Source.Target ? targetY : particlesY;
            float toY = currSource == Source.Target ? targetY : particlesY;
            blendedY = Mathf.Lerp(fromY, toY, t);
        }

        // 5) 데드존 & 스무딩
        float targetYFinal = blendedY + verticalOffset;
        float currY = transform.position.y;
        if (Mathf.Abs(targetYFinal - currY) < deadZone)
            targetYFinal = currY;

        float newY = Mathf.SmoothDamp(currY, targetYFinal, ref velY, smoothTime, maxFollowSpeed);

        if (clampY) newY = Mathf.Clamp(newY, minY, maxY);

        float newX = lockX ? fixedX : transform.position.x;
        transform.position = new Vector3(newX, newY, transform.position.z);

        lastFocusY = newY;

        // 6) 오토줌: 입자 범위로 세로 프레이밍(없으면 유지)
        if (useAutoZoom && cam)
        {
            float targetSize = cam.orthographicSize;

            if (currSource == Source.Particles && TryGetParticlesYAndExtent(out _, out float h))
            {
                // 현재 입자 세로 반높이 h 를 패딩과 함께 꼭 맞게 담는다
                float need = h * zoomPad;                  // 내용 + 패딩
                                                           // 현재 사이즈가 need 보다 작으면 키우고, 크면 서서히 최소 줌으로 돌아감
                float desiredValue = Mathf.Clamp(need, minOrthoSize, maxOrthoSize);

                // 부드럽게 보간
                targetSize = Mathf.Lerp(cam.orthographicSize, desiredValue, Time.deltaTime * zoomLerpSpeed);
            }
            else
            {
                // 타깃-only 혹은 입자 없음 → 천천히 기본 줌으로 복귀
                targetSize = Mathf.Lerp(cam.orthographicSize, minOrthoSize, Time.deltaTime * zoomLerpSpeed);
            }

            cam.orthographicSize = targetSize;
        }

    }

    // ------- 포커스 소스 계산 -------

    // 2) 이 함수 전체 교체
    bool TryGetTargetY(out float y)
    {
        y = 0f;
        if (!target || !target.gameObject.activeInHierarchy) return false;

        // (A) 렌더러가 모두 꺼져 있으면 '안 보이는 것'으로 간주
        if (treatTargetUnavailableIfRendererHidden && targetRenderers != null && targetRenderers.Length > 0)
        {
            bool anyVisible = false;
            foreach (var r in targetRenderers)
            {
                if (r && r.enabled) { anyVisible = true; break; }
            }
            if (!anyVisible) return false;
        }

        // (B) 물리가 꺼져 있으면(모핑 중) '추적 불가'로 간주
        if (treatTargetUnavailableIfPhysicsOff)
        {
            if (targetRb && !targetRb.simulated) return false;
            if (targetCol && !targetCol.enabled) return false;
        }

        y = target.position.y;
        return true;
    }


    /// <summary>
    /// 입자들의 중심 Y와 세로 반높이(= (maxY-minY)/2 )를 계산.
    /// 샘플이 부족하면 false.
    /// </summary>
    /// <summary>
    /// 활성 세트(액체 또는 기체)를 골라
    ///  - 액체면 가장 아래 입자(minY)
    ///  - 기체면 가장 위 입자(maxY)
    /// 를 포커스 Y로 반환.
    /// 또한 halfHeight = (maxY-minY)/2 를 함께 반환(오토줌용).
    /// </summary>
    bool TryGetParticlesYAndExtent(out float focusY, out float halfHeight)
    {
        focusY = 0f; halfHeight = 0f;

        // 1) 세트별 샘플 수집
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

        // 3) min/max, 질량가중 중심 계산
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
        //    centerBias=0.5면 둘 사이 정확히 중간 → 두 점이 동시에 프레이밍되기 쉬움
        focusY = Mathf.Lerp(extremeY, centroidY, Mathf.Clamp01(centerBias));

        // 5) 오토줌용 세로 반높이 계산
        //    - 전체 분포 반높이: (max-min)/2
        //    - 극단점 중심 간 거리의 반값: abs(extreme centroid)/2
        //    이 둘 중 큰 값을 채택해 두 지점이 반드시 한 화면에 들어오도록 함
        float halfBySpread = Mathf.Max(0f, (maxY - minY) * 0.5f);
        float halfByTwoPoints = Mathf.Abs(extremeY - centroidY) * 0.5f;
        float halfRaw = Mathf.Max(halfBySpread, halfByTwoPoints);

        // 6) 범위 스무딩(프레임 간 깜빡임 방지)
        // extentSmoothFactor: 0=즉시 반영, 1=매우 느림
        float lerpT = Mathf.Clamp01(1f - extentSmoothFactor);
        lastExtent = Mathf.Lerp(lastExtent <= 0f ? halfRaw : lastExtent, halfRaw, lerpT);
        halfHeight = lastExtent;

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
