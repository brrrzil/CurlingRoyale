using UnityEngine;

[CreateAssetMenu(fileName = "PhysicsConfig", menuName = "Game/PhysicsConfig")]
public class PhysicsConfig : ScriptableObject
{
    public float damping = 0.995f;
    public float bounceDamping = 0.7f;
    public float arenaRadius = 6.2f;
}