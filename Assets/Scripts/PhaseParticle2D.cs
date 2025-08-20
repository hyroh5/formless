using UnityEngine;

public enum Phase2D { Liquid, Gas }

public class PhaseParticle2D : MonoBehaviour
{
    [System.Serializable]
    public struct PhaseProps
    {
        [Header("물리")]
        public bool isKinematic;
        public float gravityScale;
        public float linearDrag;
        public float angularDrag;
        public PhysicsMaterial2D physicsMaterial;
        public bool colliderIsTrigger;

        [Header("비주얼")]
        public Sprite sprite;
        public Color color;
        public string sortingLayerName;
        public int sortingOrder;

        [Header("기타")]
        public int layer;               // -1이면 변경 안 함
        public bool enableTrail;
        public bool enableParticles;
    }

    [Header("상태별 설정")]
    public PhaseProps liquidProps = new PhaseProps
    {
        gravityScale = 1.0f,
        linearDrag = 0.1f,
        angularDrag = 0.05f,
        color = Color.white,
        layer = -1,
        enableTrail = false,
        enableParticles = false
    };
    public PhaseProps gasProps = new PhaseProps
    {
        gravityScale = -0.15f,
        linearDrag = 1.2f,
        angularDrag = 0.2f,
        color = new Color(1, 1, 1, 0.85f),
        layer = -1,
        enableTrail = true,
        enableParticles = true
    };

    [Header("초기 상태")]
    public Phase2D current = Phase2D.Liquid;

    // 캐시
    Rigidbody2D rb;
    Collider2D col;
    SpriteRenderer sr;
    TrailRenderer[] trails;
    ParticleSystem[] pss;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>(true);
        trails = GetComponentsInChildren<TrailRenderer>(true);
        pss = GetComponentsInChildren<ParticleSystem>(true);

        ApplyProps(current == Phase2D.Liquid ? liquidProps : gasProps);
    }

    public void SetPhase(Phase2D phase, Vector2 inheritVelocity = default, float kick = 0f)
    {
        current = phase;
        var props = (phase == Phase2D.Liquid) ? liquidProps : gasProps;
        ApplyProps(props);

        if (rb && kick > 0f)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            rb.velocity = inheritVelocity + dir * kick;
        }
    }

    void ApplyProps(PhaseProps p)
    {
        if (rb)
        {
            rb.isKinematic = p.isKinematic;
            rb.gravityScale = p.gravityScale;
            rb.drag = p.linearDrag;
            rb.angularDrag = p.angularDrag;
        }
        if (col)
        {
            col.isTrigger = p.colliderIsTrigger;
            if (p.physicsMaterial) col.sharedMaterial = p.physicsMaterial;
        }
        if (sr)
        {
            if (p.sprite) sr.sprite = p.sprite;
            sr.color = p.color;
            if (!string.IsNullOrEmpty(p.sortingLayerName)) sr.sortingLayerName = p.sortingLayerName;
            sr.sortingOrder = p.sortingOrder;
        }
        if (p.layer >= 0) gameObject.layer = p.layer;

        if (trails != null) foreach (var t in trails) if (t) t.enabled = p.enableTrail;

        if (pss != null)
        {
            foreach (var ps in pss) if (ps)
                {
                    if (p.enableParticles) { if (!ps.isPlaying) ps.Play(); }
                    else { if (ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
                }
        }
    }
}
