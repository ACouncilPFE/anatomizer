using UnityEngine;

namespace AIGame.Gameplay
{
    /// <summary>
    /// Scatters tears, plaque, clots, aneurysms, blockages and foreign objects over every passage
    /// of the network at the start of the mission (so the branches get some too), spaced out so you
    /// meet them one after another. Wall damage is never put where a branch opens out of the wall.
    /// </summary>
    public class ArteryLesionField : MonoBehaviour
    {
        [SerializeField] private int tears = 2;
        [SerializeField] private int plaques = 2;
        [SerializeField] private int clots = 2;
        [SerializeField] private int aneurysms = 2;
        [SerializeField] private int blockages = 1;
        [SerializeField] private int foreignObjects = 1;
        [SerializeField] private float startMargin = 18f; // keep clear of the ship's start
        [SerializeField] private float endMargin = 6f;

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

            // Lay every passage's usable length end to end, then share it out in even slots.
            float usable = 0f;
            foreach (Passage p in PassageNetwork.All)
            {
                usable += Mathf.Max(0f, Span(p));
            }
            if (usable <= 0f)
            {
                return;
            }

            float slot = usable / Mathf.Max(total, 1);
            for (int i = 0; i < total; i++)
            {
                float at = slot * (i + Random.Range(0.25f, 0.75f));
                Passage passage = PassageNetwork.Main;
                float s = 0f;
                foreach (Passage p in PassageNetwork.All)
                {
                    float span = Mathf.Max(0f, Span(p));
                    if (at <= span)
                    {
                        passage = p;
                        s = StartOf(p) + at;
                        break;
                    }
                    at -= span;
                }

                float angle = WallAngleClearOfForks(passage, s);
                GameObject go = new GameObject(order[i] + "_" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<ArteryLesion>().Configure(order[i], passage, s, angle);
            }
        }

        private float StartOf(Passage p) => p == PassageNetwork.Main ? Mathf.Max(p.MinTravel, startMargin) : p.MinTravel + 12f;
        private float Span(Passage p) => (p.MaxTravel - endMargin) - StartOf(p);

        // A spot on the wall that is solid wall, not the opening into another passage.
        private static float WallAngleClearOfForks(Passage passage, float s)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            for (int tries = 0; tries < 12; tries++)
            {
                if (!PassageNetwork.InsideAnother(passage, passage.PointAt(s, angle, 1.05f), 1f))
                {
                    break;
                }
                angle += Mathf.PI * 0.37f;
            }
            return angle;
        }
    }
}
