using System;
using System.Collections.Generic;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Sorts <see cref="IGameSubsystem" /> instances in topological order
    ///     using Kahn's algorithm, based on their declared <see cref="IGameSubsystem.Dependencies" />.
    /// </summary>
    public static class SubsystemTopologicalSorter
    {
        /// <summary>
        ///     Returns the subsystems sorted so that each subsystem appears after all
        ///     its dependencies. Throws <see cref="SubsystemCircularDependencyException" />
        ///     if a cycle is detected.
        /// </summary>
        public static List<IGameSubsystem> Sort(IReadOnlyList<IGameSubsystem> subsystems)
        {
            // Build type→subsystem lookup
            Dictionary<Type, IGameSubsystem> byType = new(subsystems.Count);

            for (int i = 0; i < subsystems.Count; i++)
            {
                Type t = subsystems[i].GetType();

                if (byType.ContainsKey(t))
                {
                    throw new InvalidOperationException(
                        $"Duplicate subsystem type: {t.Name}");
                }

                byType[t] = subsystems[i];
            }

            // Build adjacency lists and in-degree counts
            Dictionary<IGameSubsystem, List<IGameSubsystem>> dependents = new(subsystems.Count);
            Dictionary<IGameSubsystem, int> inDegree = new(subsystems.Count);

            for (int i = 0; i < subsystems.Count; i++)
            {
                dependents[subsystems[i]] = new List<IGameSubsystem>();
                inDegree[subsystems[i]] = 0;
            }

            for (int i = 0; i < subsystems.Count; i++)
            {
                IGameSubsystem sub = subsystems[i];
                IReadOnlyList<Type> deps = sub.Dependencies;

                for (int j = 0; j < deps.Count; j++)
                {
                    Type depType = deps[j];

                    if (!byType.TryGetValue(depType, out IGameSubsystem depSub))
                    {
                        // Dependency was filtered out by ShouldCreate — skip
                        continue;
                    }

                    dependents[depSub].Add(sub);
                    inDegree[sub]++;
                }
            }

            // Kahn's algorithm
            Queue<IGameSubsystem> queue = new();

            foreach (KeyValuePair<IGameSubsystem, int> kvp in inDegree)
            {
                if (kvp.Value == 0)
                {
                    queue.Enqueue(kvp.Key);
                }
            }

            List<IGameSubsystem> sorted = new(subsystems.Count);

            while (queue.Count > 0)
            {
                IGameSubsystem current = queue.Dequeue();
                sorted.Add(current);

                List<IGameSubsystem> deps = dependents[current];

                for (int i = 0; i < deps.Count; i++)
                {
                    IGameSubsystem dependent = deps[i];
                    inDegree[dependent]--;

                    if (inDegree[dependent] == 0)
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            if (sorted.Count != subsystems.Count)
            {
                // Find cycle participants for error message
                List<string> cycleMembers = new();

                foreach (KeyValuePair<IGameSubsystem, int> kvp in inDegree)
                {
                    if (kvp.Value > 0)
                    {
                        cycleMembers.Add(kvp.Key.Name);
                    }
                }

                throw new SubsystemCircularDependencyException(
                    $"Circular dependency detected among subsystems: {string.Join(", ", cycleMembers)}");
            }

            return sorted;
        }
    }
}
