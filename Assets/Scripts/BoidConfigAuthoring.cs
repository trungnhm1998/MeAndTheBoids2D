using Unity.Entities;
using UnityEngine;

public class BoidConfigAuthoring : MonoBehaviour
{
    public float MaxSpeed = 1f;
    public float MaxSteeringForce = 1f;
    public float PerceptionRadius = 1f;
    public float AvoidanceRadius = 1f;

    private class BoidConfigBaker : Baker<BoidConfigAuthoring>
    {
        public override void Bake(BoidConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BoidConfig
            {
                MaxSpeed = authoring.MaxSpeed,
                MaxSteeringForce = authoring.MaxSteeringForce,
                PerceptionRadius = authoring.PerceptionRadius,
                AvoidanceRadius = authoring.AvoidanceRadius
            });
        }
    }
}

public struct BoidConfig : IComponentData
{
    public float MaxSpeed;
    public float MaxSteeringForce;
    public float PerceptionRadius;
    public float AvoidanceRadius;
}