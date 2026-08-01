using System;

namespace NaughtyAttributes
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true, Inherited = true)]
    public class OnValueChangedAttribute : MetaAttribute
    {
        public string CallbackName { get; private set; }
        public bool IncludeChildren { get; private set; }

        public OnValueChangedAttribute(string callbackName)
        {
            CallbackName = callbackName;
        }

        public OnValueChangedAttribute(string callbackName, bool includeChildren)
        {
            CallbackName = callbackName;
            IncludeChildren = includeChildren;
        }
    }
}
