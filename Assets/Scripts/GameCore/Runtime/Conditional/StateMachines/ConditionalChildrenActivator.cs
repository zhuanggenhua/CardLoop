using UnityEngine;

namespace FantasyWord.GameCore
{
    public class ConditionalChildrenActivator : AConditionalActivator
    {
        protected override void Activate(bool state)
        {
            int childCount = transform.childCount;

            for (int i = 0; i < childCount; ++i)
            {
                Transform child = transform.GetChild(i);
                child.gameObject.SetActive(state);
            }
        }
    }
}

