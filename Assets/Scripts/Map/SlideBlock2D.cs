using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SlideBlock2D : MonoBehaviour
{
    public enum SlideMode { Toggle, Hold, OneShot }

    [Header("�⺻ �̵� ����")]
    public Vector2 direction = Vector2.right;  // �̵� ����(����)
    public float distance = 2f;                // �̵� �Ÿ�
    public float speed = 3f;                   // m/s
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("���� ���")]
    public SlideMode mode = SlideMode.Toggle;
    public bool startOpened = false;           // ���� ����(����=�� ��ġ)

    [Header("�ΰ� �ɼ�")]
    public float startDelay = 0f;              // ���� �� ����
    public float returnDelay = 0f;             // Hold���� �� ���� ������ �� ����
    public bool autoReturnInToggle = false;    // Toggle������ ���� �ð� �� �ڵ� ����
    public float autoReturnDelay = 1.5f;

    [Header("���� ����(����)")]
    public bool useRigidbodyIfPresent = true;

    // ---- ���� ----
    Vector3 startPos, endPos;
    Rigidbody2D rb;
    Coroutine playCo;
    bool opened;       // ���� ���� ����
    bool locked;       // OneShot���� ������ ���

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

    // --- �ܺ� ȣ��� ---
    public void PressDown()  // ��ư ���� ����
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
                locked = true; // �ٽ� ������ ����
            }
        }
    }

    public void PressUp()    // ��ư���� �� ���� ��
    {
        if (locked) return;
        if (mode == SlideMode.Hold)
        {
            if (returnDelay > 0f) StartCoroutine(CoReturnAfterDelay());
            else MoveTo(false);
        }
        // Toggle/OneShot�� ����
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
