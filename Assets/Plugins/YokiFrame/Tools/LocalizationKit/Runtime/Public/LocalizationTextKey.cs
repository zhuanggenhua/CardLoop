using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Strongly typed localization text id for LocalizationKit generated code.
    /// </summary>
    public readonly struct LocalizationTextKey
    {
        public LocalizationTextKey(int id)
        {
            Id = id;
        }

        public int Id { get; }
        public bool IsValid => Id > 0;

        public string Get() => LocalizationKit.Get(Id);
        public string Get(LanguageId languageId) => LocalizationKit.Get(languageId, Id);
        public string Format(params object[] args) => LocalizationKit.Get(Id, args);
        public string Format(IReadOnlyDictionary<string, object> args) => LocalizationKit.Get(Id, args);
        public string GetPlural(int count) => LocalizationKit.GetPlural(Id, count);
        public string GetPlural(int count, params object[] extraArgs) => LocalizationKit.GetPlural(Id, count, extraArgs);

        public override string ToString() => Id.ToString();
        public static implicit operator int(LocalizationTextKey key) => key.Id;
    }
}
