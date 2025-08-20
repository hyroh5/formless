using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlideBlock2D : MonoBehaviour
{
    public enum SlideMode { Toggle, Hold, OneShot }

    [Header("기본 이동 설정")]
    public Vector2 direction = Vector2.right;  // 이동 방향(월드)
    public float distance = 2f;                // 이동 거리
    public float speed = 3f;                   // m/s
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("동작 모드")]
    public SlideMode mode = SlideMode.Toggle;
    public bool startOpened = false;           // 시작 상태(열림=끝 위치)

    [Header("부가 옵션")]
    public float startDelay = 0f;              // 동작 전 지연
    public float returnDelay = 0f;             // Hold에서 손 떼면 닫히기 전 지연
    public bool autoReturnInToggle = false;    // Toggle에서도 일정 시간 뒤 자동 복귀
    public float autoReturnDelay = 1.5f;

    [Header("물리 연동(선택)")]
    public bool useRigidbodyIfPresent = true;

    // ---- 내부 ----
    Vector3 startPos, endPos;
    Rigidbody2D rb;
    Coroutine playCo;
    bool opened;       // 현재 열림 여부
    bool locked;       // OneShot으로 열리고 잠금

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        startPos = transform.position;
        endPos = startPos + (Vector3)(direction.normalized * distance);
        opened = startOpened;

        if (startOpened) Snap(endPos);
        else Snap(startPos);
    }

    void Snap(Vector3 p)
    {
        if (rb && useRigidbodyIfPresent) rb.position = p;
        else transform.position = p;
    }

    // --- 외부 호출용 ---
    public void PressDown()  // 버튼 눌림 시작
    {
        if (locked) return;

        if (mode == SlideMode.Hold)
        {
            MoveTo(true);
        }
        else if (mode == SlideMode.Toggle)
        {
            MoveTo(!opened);
            if (autoReturnInToggle)
                StartCoroutine(CoAutoReturn());
        }
        else if (mode == SlideMode.OneShot)
        {
            if (!opened)
            {
                MoveTo(true);
                locked = true; // 다시 닫히지 않음
            }
        }
    }

    public void PressUp()    // 버튼에서 발 뗐을 때
    {
        if (locked) return;
        if (mode == SlideMode.Hold)
        {
            if (returnDelay > 0f) StartCoroutine(CoReturnAfterDelay());
            else MoveTo(false);
        }
        // Toggle/OneShot은 무시
    }

    IEnumerator CoAutoReturn()
    {
        yield return new WaitForSeconds(autoReturnDelay);
        if (mode == SlideMode.Toggle && opened) MoveTo(false);
    }

    IEnumerator CoReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        MoveTo(false);
    }

    void MoveTo(bool open)
    {
        if (playCo != null) StopCoroutine(playCo);
        playCo = StartCoroutine(CoMove(open));
    }

    IEnumerator CoMove(bool toOpen)
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        Vector3 from = (rb && useRigidbodyIfPresent) ? (Vector3)rb.position : transform.position;
        Vector3 to = toOpen ? endPos : startPos;

        float dist = Vector3.Distance(from, to);
        float dur = (speed <= 0.0001f) ? 0f : dist / speed;
        float t = 0f;

        while (t < dur)
        {
            float u = ease.Evaluate(Mathf.Clamp01(t / Mathf.Max(dur, 0.0001f)));
            Vector3 p = Vector3.Lerp(from, to, u);

            if (rb && useRigidbodyIfPresent) rb.MovePosition(p);
            else transform.position = p;

            t += Time.deltaTime;
            yield return null;
        }

        Snap(to);
        opened = toOpen;
        playCo = null;
    }
}
