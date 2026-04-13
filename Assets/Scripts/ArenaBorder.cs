using UnityEngine;

[RequireComponent(typeof(EdgeCollider2D))]
public class ArenaBorder : MonoBehaviour
{
    public float radius = 5f;
    public int points = 64;

    void Start()
    {
        EdgeCollider2D edge = GetComponent<EdgeCollider2D>();
        Vector2[] pointsArray = new Vector2[points + 1];

        for (int i = 0; i <= points; i++)
        {
            float angle = (float)i / points * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;
            pointsArray[i] = new Vector2(x, y);
        }

        edge.points = pointsArray;
    }
}