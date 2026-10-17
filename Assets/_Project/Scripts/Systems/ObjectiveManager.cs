using AIGame.Core;
using System.Collections.Generic;
using UnityEngine;

namespace AIGame.Systems
{
    public class ObjectiveManager : MonoBehaviour
    {
        private readonly HashSet<ObjectiveNode> nodes = new HashSet<ObjectiveNode>();
        private int completed;

        public void RegisterNode(ObjectiveNode node)
        {
            if (node == null)
            {
                return;
            }

            nodes.Add(node);
            GameEvents.RaiseObjectiveProgressChanged(completed, nodes.Count);
        }

        public void MarkCompleted(ObjectiveNode node)
        {
            if (node == null || !nodes.Contains(node) || node.IsCompleted)
            {
                return;
            }

            node.SetCompleted();
            completed++;
            GameEvents.RaiseObjectiveProgressChanged(completed, nodes.Count);
            GameManager.Instance?.RegisterObjectiveComplete();
        }
    }
}
