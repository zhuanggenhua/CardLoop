using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LabelAttribute : MetaAttribute
    {
        public string Label { get; private set; }

        public LabelAttribute(string label)
        {
            Label = label;
        }
    }
}
