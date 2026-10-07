using UnityEngine;

/// <summary>
/// Lightweight carrier component stamped onto spawned objects by ParallaxSpawnSystem.
/// Stores the world-space reflection position offset configured per spawn entry so that
/// any script on the same GameObject (e.g. enemy state machines) can read it at runtime
/// without needing their own Inspector field.
/// </summary>
public class ReflectionOffsetComponent : MonoBehaviour
{
    /// <summary>World-space offset forwarded to ReflectionSystem calls on this object.</summary>
    [HideInInspector] public Vector3 offset;
}
