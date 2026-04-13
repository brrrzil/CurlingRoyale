using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Настройки удара")]
    public float minForce = 3f;
    public float maxForce = 16f;
    public float maxChargeTime = 2f;

    [Header("Визуал")]
    public LineRenderer lineRenderer;
    public Transform chargeCircle;

    private CustomPhysicsBody physicsBody;
    private Vector2 direction;
    private float chargeStartTime;
    private bool isCharging;

    void Start()
    {
        physicsBody = GetComponent<CustomPhysicsBody>();

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (chargeCircle != null)
            chargeCircle.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            if (Vector2.Distance(mousePos, transform.position) < 1f)
            {
                StartCharge();
            }
        }

        if (isCharging && Input.GetMouseButtonUp(0))
        {
            ReleaseCharge();
        }

        if (isCharging)
        {
            UpdateChargeVisual();
        }
    }

    void StartCharge()
    {
        isCharging = true;
        chargeStartTime = Time.time;

        if (lineRenderer != null)
            lineRenderer.enabled = true;

        if (chargeCircle != null)
            chargeCircle.gameObject.SetActive(true);
    }

    void ReleaseCharge()
    {
        isCharging = false;

        if (lineRenderer != null)
            lineRenderer.enabled = false;

        if (chargeCircle != null)
            chargeCircle.gameObject.SetActive(false);

        float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
        float force = Mathf.Lerp(minForce, maxForce, chargeTime / maxChargeTime);

        physicsBody.ApplyForce(direction, force);
    }

    void UpdateChargeVisual()
    {
        Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector2 pullDirection = currentMousePos - (Vector2)transform.position;
        direction = -pullDirection.normalized;

        float chargeTime = Mathf.Min(Time.time - chargeStartTime, maxChargeTime);
        float t = chargeTime / maxChargeTime;
        float lineLength = Mathf.Lerp(0.5f, 3f, t);

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, transform.position + (Vector3)direction * lineLength);
        }

        if (chargeCircle != null)
        {
            float scale = Mathf.Lerp(0.5f, 1.5f, t);
            chargeCircle.localScale = Vector3.one * scale;

            SpriteRenderer sr = chargeCircle.GetComponent<SpriteRenderer>();
            if (sr != null)
                sr.color = Color.Lerp(Color.green, Color.red, t);
        }
    }
}