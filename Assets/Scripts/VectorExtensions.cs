using Unity.Mathematics;

public static class VectorExtensions
{
    public static float3 Limit(this float3 vector, float maxLength)
    {
        if (math.length(vector) > maxLength)
        {
            vector = math.normalize(vector) * maxLength;
        }

        return vector;
    }
}