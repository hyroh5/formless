using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlideBlock2D : MonoBehaviour
{
    public enum SlideMode { Toggle, Hold, OneShot }

    [Header("�̵� ���")]
    public Vector2 direction = Vector2.right;
    public float distance = 2f;

    [Header("�ִϸ��̼� �ð� / ��¡")]
    public float openDuration = 0.55f;
    public float closeDuration = 0.45f;
    public AnimationCurve openCurve = null;   // �⺻: EaseInOut
    public AnimationCurve closeCurve = null;

    [Header("���� ���")]
    public SlideMode mode = SlideMode.Toggle;
    public bool startOpened = false;

    [Header("�ΰ� �ɼ�")]
    public float startDelay = 0f;
    public float returnDelay = 0f;          // Hold�� �� �� ���� ���������
    public bool useRigidbodyIfPresent = true;
    public bool unscaledTime = false;       // �Ͻ����� ���� ��� ���
    public bool addSmoothDamp = true;       // Ŀ�꿡 �� �� �� �ε巯��

    // ----- ���� -----
    Vector3 pStart, pEnd;
    Rigidbody2D rb;
    Coroutine playCo;
    bool opened;     // ���� �� ����

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

    // �ܺο��� ȣ��
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

        // ���� ��ġ���� ���� (�߰� ��ȯ ����)
        Vector3 from = rb && useRigidbodyIfPresent ? (Vector3)rb.position : transform.position;
        Vector3 to = toOpen ? pEnd : pStart;

        float totalDist = Vector3.Distance(from, to);
        if (totalDist < 0.0001f) { Snap(to); opened = toOpen; yield break; }

        // ���� �Ÿ� ������ŭ ���� duration ���/Ȯ��
        float baseDur = toOpen ? openDuration : closeDuration;
        float fullDist = Vector3.Distance(pStart, pEnd);
        float dur = baseDur * Mathf.Clamp01(totalDist / Mathf.Max(0.0001f, fullDist));

        var curve = toOpen ? openCurve : closeCurve;
        float t = 0f;
        Vector3 vel = Vector3.zero; // SmoothDamp��

        // Rigidbody��� FixedUpdate Ÿ�̹� ���
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

            // FixedUpdate ������ �� (������ ��ũ)
            yield return null;
        }

        Snap(to);
        opened = toOpen;
        playCo = null;
    }
}
