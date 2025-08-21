using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlideBlock2D : MonoBehaviour
{
    public enum SlideMode { Toggle, Hold, OneShot }

    [Header("이동 경로")]
    public Vector2 direction = Vector2.right;
    public float distance = 2f;

    [Header("애니메이션 시간 / 이징")]
    public float openDuration = 0.55f;
    public float closeDuration = 0.45f;
    public AnimationCurve openCurve = null;   // 기본: EaseInOut
    public AnimationCurve closeCurve = null;

    [Header("동작 모드")]
    public SlideMode mode = SlideMode.Toggle;
    public bool startOpened = false;

    [Header("부가 옵션")]
    public float startDelay = 0f;
    public float returnDelay = 0f;          // Hold일 때 발 떼고 닫히기까지
    public bool useRigidbodyIfPresent = true;
    public bool unscaledTime = false;       // 일시정지 연출 등에서 사용
    public bool addSmoothDamp = true;       // 커브에 한 번 더 부드러움

    // ----- 내부 -----
    Vector3 pStart, pEnd;
    Rigidbody2D rb;
    Coroutine playCo;
    bool opened;     // 현재 논리 상태

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pStart = transform.position;
        pEnd = pStart + (Vector3)(direction.normalized * distance);
        opened = startOpened;

        if (openCurve == null) openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        if (closeCurve == null) closeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Snap(opened ? pEnd : pStart);
    }

    void Snap(Vector3 p)
    {
        if (rb && useRigidbodyIfPresent) rb.position = p;
        else transform.position = p;
    }

    // 외부에서 호출
    public void PressDown()
    {
        if (mode == SlideMode.Hold) MoveTo(true);
        else if (mode == SlideMode.Toggle) MoveTo(!opened);
        else if (mode == SlideMode.OneShot && !opened) MoveTo(true);
    }
    public void PressUp()
    {
        if (mode == SlideMode.Hold)
        {
            if (returnDelay > 0f) StartCoroutine(CoDelayThen(() => MoveTo(false), returnDelay));
            else MoveTo(false);
        }
    }

    IEnumerator CoDelayThen(System.Action act, float d)
    {
        if (unscaledTime) yield return new WaitForSecondsRealtime(d);
        else yield return new WaitForSeconds(d);
        act?.Invoke();
    }

    void MoveTo(bool toOpen)
    {
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(CoMove(toOpen));
    }

    IEnumerator CoMove(bool toOpen)
    {
        if (startDelay > 0f)
        {
            if (unscaledTime) yield return new WaitForSecondsRealtime(startDelay);
            else yield return new WaitForSeconds(startDelay);
        }

        // 현재 위치에서 시작 (중간 전환 지원)
        Vector3 from = rb && useRigidbodyIfPresent ? (Vector3)rb.position : transform.position;
        Vector3 to = toOpen ? pEnd : pStart;

        float totalDist = Vector3.Distance(from, to);
        if (totalDist < 0.0001f) { Snap(to); opened = toOpen; yield break; }

        // 남은 거리 비율만큼 실제 duration 축소/확장
        float baseDur = toOpen ? openDuration : closeDuration;
        float fullDist = Vector3.Distance(pStart, pEnd);
        float dur = baseDur * Mathf.Clamp01(totalDist / Mathf.Max(0.0001f, fullDist));

        var curve = toOpen ? openCurve : closeCurve;
        float t = 0f;
        Vector3 vel = Vector3.zero; // SmoothDamp용

        // Rigidbody라면 FixedUpdate 타이밍 사용
        while (t < dur)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;
            float u = Mathf.Clamp01(t / dur);
            float w = curve.Evaluate(u);

            Vector3 target = Vector3.Lerp(from, to, w);

            if (addSmoothDamp)
                target = Vector3.SmoothDamp(
                    rb && useRigidbodyIfPresent ? (Vector3)rb.position : transform.position,
                    target, ref vel, 0.06f, Mathf.Infinity, dt);

            if (rb && useRigidbodyIfPresent) rb.MovePosition(target);
            else transform.position = target;

            // FixedUpdate 느낌을 줌 (물리와 싱크)
            yield return null;
        }

        Snap(to);
        opened = toOpen;
        playCo = null;
    }
}
