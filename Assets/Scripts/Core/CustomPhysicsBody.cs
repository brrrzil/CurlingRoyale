using UnityEngine;

public class CustomPhysicsBody : MonoBehaviour
{
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void ApplyForce(Vector2 direction, float force)
    {
        if (rb != null)
            rb.linearVelocity = direction * force;
    }

    public Vector2 GetVelocity()
    {
        return rb != null ? rb.linearVelocity : Vector2.zero;
    }
}