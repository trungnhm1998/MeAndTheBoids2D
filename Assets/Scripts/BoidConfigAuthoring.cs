using Unity.Entities;
using UnityEngine;

public class BoidConfigAuthoring : MonoBehaviour
{
    public bool EnableBoid = false;
    [Range(0, 5)]
    public float AlignmentWeight = 1f;
    [Range(0, 5)]
    public float CohesionWeight = 1f;
    [Range(0, 5)]
    public float SeparationWeight = 1f;
    public float FieldOfView = 1f;
    public float MaxSpeed = 1f;
    public float MaxSteeringForce = 1f;
    public float PerceptionRadius = 1f;
    public bool ShowPerceptionRadius = true;

    private class BoidConfigBaker : Baker<BoidConfigAuthoring>
    {
        public override void Bake(BoidConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BoidConfig
            {
                EnableBoid = authoring.EnableBoid,
                
                AlignmentWeight = authoring.AlignmentWeight,
                CohesionWeight = authoring.CohesionWeight,
                SeparationWeight = authoring.SeparationWeight,
                
                FieldOfView = authoring.FieldOfView,
                
                MaxSpeed = authoring.MaxSpeed,
                MaxSteeringForce = authoring.MaxSteeringForce,
                PerceptionRadius = authoring.PerceptionRadius,
                ShowPerceptionRadius = authoring.ShowPerceptionRadius
            });
        }
    }
}

public struct BoidConfig : IComponentData
{
    public bool EnableBoid;
    
    public float MaxSpeed;
    public float MaxSteeringForce;
    public float PerceptionRadius;
    public bool ShowPerceptionRadius;
    public float AlignmentWeight;
    public float CohesionWeight;
    public float SeparationWeight;
    public float FieldOfView;
}