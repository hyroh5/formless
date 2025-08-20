using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Button2D : MonoBehaviour
{
    [Header("연결된 슬라이드 블록들")]
    public List<SlideBlock2D> targets = new();

    [Header("필터(옵션)")]
    public LayerMask activatorLayers = ~0; // 전부 허용
    public string requiredTag = "";        // 비워두면 태그 무시

    [Header("반복 입력 허용")]
    public bool holdToKeepPressed = true;  // 밟는 동안만 눌림 상태 유지

    [Header("연출(옵션)")]
    public Animator animator;              // "Pressed" bool 파라미터 사용
    public AudioSource sfxDown, sfxUp;

    int insideCount = 0;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true; // 버튼은 트리거 모드를 권장
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
        // TODO: 색 바꾸기 등 시각 연출 추가 가능
    }

    void PressUp()
    {
        foreach (var t in targets) if (t) t.PressUp();
        if (animator) animator.SetBool("Pressed", false);
        if (sfxUp) sfxUp.Play();
    }
}
