using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SolidMovement2D : MonoBehaviour
{
    [Header("이동/점프")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("지면 체크")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    Rigidbody2D rb;
    Collider2D col;
    bool controlsEnabled = true;
    bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    void Update()
    {
        if (!controlsEnabled) return;

        // 좌우 이동
        float move = Input.GetAxisRaw("Horizontal");
        if (move != 0f)
            rb.velocity = new Vector2(move * moveSpeed, rb.velocity.y);

        // 점프
        if (groundCheck)
            isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
    }

    public void EnableControls(bool enable)
    {
        controlsEnabled = enable;
    }

    public void FreezePhysics(bool freeze)
    {
        if (rb) rb.simulated = !freeze;
        if (col) col.enabled = !freeze;
        if (freeze && rb) rb.velocity = Vector2.zero;
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}

