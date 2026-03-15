using UnityEngine;

namespace Lithforge.Runtime.Content.Behaviors
{
    /// <summary>
    /// Base class for all block behavior actions. Subclasses define what happens
    /// when a <see cref="Blocks.BlockBehavior"/> is triggered (e.g. give item, play sound, set block).
    /// </summary>
    public abstract class BehaviorAction : ScriptableObject
    {
    }
}
