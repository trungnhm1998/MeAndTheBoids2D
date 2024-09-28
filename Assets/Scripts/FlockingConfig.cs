using Unity.Entities;
using UnityEngine;

public class FlockingConfig : MonoBehaviour
{
    private class FlockingConfigBaker : Baker<FlockingConfig>
    {
        public override void Bake(FlockingConfig authoring) { }
    }
}