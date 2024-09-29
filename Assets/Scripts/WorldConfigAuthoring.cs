using Drawing;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WorldConfigAuthoring : MonoBehaviourGizmos
{
    [SerializeField] private Vector2 _size = new(100, 100);
    public Vector2 Size => _size;
    public Vector2 Bounds => _size;
    public Vector2 BottomLeft => new Vector2(transform.position.x, transform.position.y) - _size / 2;
    public Vector2 TopRight => new Vector2(transform.position.x, transform.position.y) + _size / 2;
    public Vector2 TopLeft => new Vector2(transform.position.x, transform.position.y) + new Vector2(-_size.x / 2, _size.y / 2);
    public Vector2 BottomRight => new Vector2(transform.position.x, transform.position.y) + new Vector2(_size.x / 2, -_size.y / 2);

    private void Start()
    {
        Application.targetFrameRate = -1;
    }

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

    public override void DrawGizmos()
    {

        Vector3 topLeft = transform.position + new Vector3(-_size.x / 2, _size.y / 2, 0);
        Vector3 topRight = transform.position + new Vector3(_size.x / 2, _size.y / 2, 0);
        Vector3 bottomLeft = transform.position + new Vector3(-_size.x / 2, -_size.y / 2, 0);
        Vector3 bottomRight = transform.position + new Vector3(_size.x / 2, -_size.y / 2, 0);

        var draw = Draw.xy;
        draw.PushColor(Color.green);
        draw.Line(topLeft, topRight);
        draw.Line(topRight, bottomRight);
        draw.Line(bottomRight, bottomLeft);
        draw.Line(bottomLeft, topLeft);
        draw.PopColor();
    }
}

public struct WorldConfig : IComponentData
{
    public float2 Bound;
    public float2 BottomLeft => new float2(-Bound.x / 2, -Bound.y / 2);
    public float2 TopRight => new float2(Bound.x / 2, Bound.y / 2);
}