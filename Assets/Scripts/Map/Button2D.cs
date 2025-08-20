using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Button2D : MonoBehaviour
{
    [Header("����� �����̵� ��ϵ�")]
    public List<SlideBlock2D> targets = new();

    [Header("����(�ɼ�)")]
    public LayerMask activatorLayers = ~0; // ���� ���
    public string requiredTag = "";        // ����θ� �±� ����

    [Header("�ݺ� �Է� ���")]
    public bool holdToKeepPressed = true;  // ��� ���ȸ� ���� ���� ����

    [Header("����(�ɼ�)")]
    public Animator animator;              // "Pressed" bool �Ķ���� ���
    public AudioSource sfxDown, sfxUp;

    int insideCount = 0;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // ��ư�� Ʈ���� ��带 ����
    }

    bool PassesFilter(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & activatorLayers) == 0) return false;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return false;
        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;
        insideCount++;
        if (insideCount == 1) PressDown();
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!PassesFilter(other)) return;
        insideCount = Mathf.Max(0, insideCount - 1);
        if (insideCount == 0 && holdToKeepPressed) PressUp();
    }

    void PressDown()
    {
        foreach (var t in targets) if (t) t.PressDown();
        if (animator) animator.SetBool("Pressed", true);
        if (sfxDown) sfxDown.Play();
        // TODO: �� �ٲٱ� �� �ð� ���� �߰� ����
    }

    void PressUp()
    {
        foreach (var t in targets) if (t) t.PressUp();
        if (animator) animator.SetBool("Pressed", false);
        if (sfxUp) sfxUp.Play();
    }
}
