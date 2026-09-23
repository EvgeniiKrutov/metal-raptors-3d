using UnityEngine;

namespace MetalRaptors
{
    public interface ISolid
    {
        bool Repel(ref Vector3 position, ref Vector3 velocity, float radius);
    }
}
