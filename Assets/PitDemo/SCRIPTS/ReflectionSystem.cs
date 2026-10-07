using UnityEngine;

// ARcade's copy of THE PIT has no water, so there's no reflection system:
// Instance is always null and every ReflectionSystem.Instance?.X call in the
// ported scripts is skipped. Those calls only spawn the mirrored copies;
// the effects themselves are spawned separately.
public class ReflectionSystem : MonoBehaviour
{
    public static ReflectionSystem Instance => null;

    public void RegisterSource(SpriteRenderer source) { }
    public void RegisterSource(SpriteRenderer source, Vector3 positionOffset) { }
    public void SpawnReflectedParticle(GameObject prefab, Vector3 worldPosition, Quaternion rotation) { }
    public void SpawnReflectedParticle(GameObject prefab, Vector3 worldPosition, Quaternion rotation, Vector3 positionOffset) { }
}
