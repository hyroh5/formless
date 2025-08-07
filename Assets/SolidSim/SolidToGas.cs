using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SolidToGas_ReusePool : MonoBehaviour
{
    [Header("기체 입자 풀 (씬에 미리 배치됨)")]
    public List<GameObject> gasParticles = new List<GameObject>();

    [Header("고체 이동 속도")]
    public float moveSpeed = 5f;

    [Header("기체로 변환 시 몇 개 활성화할지")]
    public int spawnCount = 20;
    public float spawnRadius = 0.5f;
    public float initialForce = 2f;

    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        float move = Input.GetAxisRaw("Horizontal");
        rb.velocity = new Vector2(move * moveSpeed, rb.velocity.y);

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

#if UNITY_EDITOR
[CustomEditor(typeof(SolidToGas_ReusePool))]
public class SolidToGasEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SolidToGas_ReusePool script = (SolidToGas_ReusePool)target;

        if (GUILayout.Button("🔄 Hierarchy에서 기체 자동 등록"))
        {
            GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
            script.gasParticles = new List<GameObject>();

            foreach (GameObject obj in allObjects)
            {
                if (obj.name.Contains("GasParticle")) // 이름 조건은 필요에 따라 수정
                {
                    script.gasParticles.Add(obj);
                }
            }

            Debug.Log($"✅ {script.gasParticles.Count}개의 기체 입자를 등록했습니다.");
            EditorUtility.SetDirty(script);
        }
    }
}
#endif
