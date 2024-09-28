using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WorldConfigAuthoring : MonoBehaviour
{
    [SerializeField] private Vector2 _size = new(100, 100);

    private class Baker : Baker<WorldConfigAuthoring>
    {
        public override void Bake(WorldConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new WorldConfig
            {
                Bound = authoring._size
            });
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;

        Vector3 topLeft = transform.position + new Vector3(-_size.x / 2, _size.y / 2, 0);
        Vector3 topRight = transform.position + new Vector3(_size.x / 2, _size.y / 2, 0);
        Vector3 bottomLeft = transform.position + new Vector3(-_size.x / 2, -_size.y / 2, 0);
        Vector3 bottomRight = transform.position + new Vector3(_size.x / 2, -_size.y / 2, 0);

        Gizmos.DrawLine(topLeft, topRight);
        Gizmos.DrawLine(topRight, bottomRight);
        Gizmos.DrawLine(bottomRight, bottomLeft);
        Gizmos.DrawLine(bottomLeft, topLeft);
    }
}

public struct WorldConfig : IComponentData
{
    public float2 Bound;
}