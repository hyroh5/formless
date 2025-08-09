using UnityEngine;

public class SolidToLiquid : MonoBehaviour
{
    public GameObject particlePrefab;
    public int particlesPerRow = 500;
    public int particlesPerColumn = 500;

    public void BreakIntoLiquid()
    {
        Vector2 center = transform.position;
        float spacing = Config.SPACING;

        for (int i = 0; i < particlesPerRow; i++)
        {
            for (int j = 0; j < particlesPerColumn; j++)
            {
                Vector2 pos = center + new Vector2(
                    (i - particlesPerRow / 2f) * spacing,
                    (j - particlesPerColumn / 2f) * spacing
                );

                Instantiate(particlePrefab, pos, Quaternion.identity);
            }
        }

        Destroy(gameObject);  // 고체 제거
    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            BreakIntoLiquid();
        }
    }

}
