using UnityEngine;

namespace AIGame.Systems
{
    public class ObjectiveNode : MonoBehaviour
    {
        [SerializeField] private bool isCompleted;

        public bool IsCompleted => isCompleted;

        public void SetCompleted()
        {
            isCompleted = true;
        }
    }
}
