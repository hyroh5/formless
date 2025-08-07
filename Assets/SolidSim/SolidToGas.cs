using System.Collections.Generic;
using UnityEngine;

public class SolidToGas_ReusePool : MonoBehaviour
{
    [Header("기체 입자 풀 (씬에 미리 배치됨)")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("고체 이동 속도 / 점프 힘")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;

    [Header("기체로 변환 시 몇 개 활성화할지")]
    public int spawnCount = 20;
    public float spawnRadius = 0.5f;
    public float initialForce = 2f;

    private Rigidbody2D rb;
    private bool isGrounded;

    [Header("바닥 체크 설정")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.1f;
    public LayerMask groundLayer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 바닥에 닿았는지 체크
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        float move = Input.GetAxisRaw("Horizontal");

        // 이동 입력 있을 때만 속도 변경 (경사면 미끄럼 방해 X)
        if (move != 0)
        {
            rb.velocity = new Vector2(move * moveSpeed, rb.velocity.y);
        }

        // 점프 입력
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        }

        // 기체 변환
        if (Input.GetKeyDown(KeyCode.X))
        {
            TransformToGas();
        }
    }

    void TransformToGas()
    {
        int spawned = 0;

        foreach (GameObject gas in gasParticles)
        {
            if (!gas.activeInHierarchy)
            {
                Vector2 offset = Random.insideUnitCircle * spawnRadius;
                gas.transform.position = transform.position + (Vector3)offset;
                gas.SetActive(true);

                Rigidbody2D gasRb = gas.GetComponent<Rigidbody2D>();
                if (gasRb != null)
                {
                    Vector2 randomDir = Random.insideUnitCircle.normalized;
                    gasRb.velocity = randomDir * initialForce;
                }

                spawned++;
                if (spawned >= spawnCount)
                    break;
            }
        }

        Destroy(gameObject);
    }
}
