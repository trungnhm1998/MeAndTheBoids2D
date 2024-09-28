using Unity.Entities;
using UnityEngine;

public class BoidsVisualAuthoring : MonoBehaviour
{
    public Texture2D BoidTexture;
    private class BoidsVisualAuthoringBaker : Baker<BoidsVisualAuthoring>
    {
        public override void Bake(BoidsVisualAuthoring authoring)
        {
            var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new BoidsVisual()
            {
                BoidTexture = authoring.BoidTexture
            });
        }
    }
}

public class BoidsVisual : IComponentData
{
    public Texture2D BoidTexture;
}