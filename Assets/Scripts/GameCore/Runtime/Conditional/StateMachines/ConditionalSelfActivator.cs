namespace FantasyWord.GameCore
{
    public class ConditionalSelfActivator : AConditionalActivator
    {
        protected override void Activate(bool state)
        {
            gameObject.SetActive(state);
        }
    }
}

