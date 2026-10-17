using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Scatters tears, plaque, clots, aneurysms, blockages and foreign objects along the artery at
    /// the start of the mission, spaced out so you meet them one after another down the vessel.
    /// </summary>
    public class ArteryLesionField : MonoBehaviour
    {
        [SerializeField] private int tears = 2;
        [SerializeField] private int plaques = 2;
        [SerializeField] private int clots = 2;
        [SerializeField] private int aneurysms = 2;
        [SerializeField] private int blockages = 1;
        [SerializeField] private int foreignObjects = 1;
        [SerializeField] private Vector2 zRange = new Vector2(18f, 145f);

        private void Start()
        {
            int total = tears + plaques + clots + aneurysms + blockages + foreignObjects;
            LesionType[] order = new LesionType[total];
            int n = 0;
            for (int i = 0; i < tears; i++)   order[n++] = LesionType.Tear;
            for (int i = 0; i < plaques; i++) order[n++] = LesionType.Plaque;
            for (int i = 0; i < clots; i++)   order[n++] = LesionType.Clot;
            for (int i = 0; i < aneurysms; i++) order[n++] = LesionType.Aneurysm;
            for (int i = 0; i < blockages; i++) order[n++] = LesionType.Blockage;
            for (int i = 0; i < foreignObjects; i++) order[n++] = LesionType.ForeignObject;

            // Shuffle, so the types are mixed along the vessel.
            for (int i = total - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            float slot = (zRange.y - zRange.x) / Mathf.Max(total, 1);
            for (int i = 0; i < total; i++)
            {
                float z = zRange.x + slot * (i + Random.Range(0.25f, 0.75f));
                float angle = Random.Range(0f, Mathf.PI * 2f);
                GameObject go = new GameObject(order[i] + "_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<ArteryLesion>().Configure(order[i], z, angle);
            }
        }
    }
}
