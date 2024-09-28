using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class BoidAuthoring : MonoBehaviour
{
    [SerializeField] private Sprite _sprite;
    
    private class Baker : Baker<BoidAuthoring>
    {
        public override void Bake(BoidAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponent(entity, new Boid
            {
                Acceleration = float3.zero,
                Velocity = float3.zero,
            });
            // AddComponent(entity, new );
        }
    }
}

public struct Boid : IComponentData
{
    public int Id;
    public float3 Acceleration;
    public float3 Velocity;
}