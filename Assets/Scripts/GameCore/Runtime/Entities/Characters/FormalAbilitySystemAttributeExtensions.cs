using GAS.Runtime;
using Unity.Entities;
using UEntity = Unity.Entities.Entity;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// GameCore 对 EX-GAS 2.0 属性写入的项目侧适配。
    /// GAS2 官方只公开基础值写入；读档、受击和恢复等当前值写入语义保留在项目侧，不改第三方插件源码。
    /// </summary>
    internal static class FormalAbilitySystemAttributeExtensions
    {
        public static void SetAttrValues(
            this AbilitySystemComponent abilitySystemComponent,
            int attrSetCode,
            int attributeCode,
            float baseValue,
            float currentValue)
        {
            if (abilitySystemComponent == null)
            {
                return;
            }

            abilitySystemComponent.SetAttrBaseValue(attrSetCode, attributeCode, baseValue);
            abilitySystemComponent.SetAttrCurrentValue(attrSetCode, attributeCode, currentValue);
        }

        public static void SetAttrCurrentValue(
            this AbilitySystemComponent abilitySystemComponent,
            int attrSetCode,
            int attributeCode,
            float currentValue)
        {
            if (abilitySystemComponent == null)
            {
                return;
            }

            UEntity ascEntity = abilitySystemComponent.Cell.Entity;
            EntityManager entityManager = GASManager.EntityManager;
            if (ascEntity == UEntity.Null ||
                !entityManager.Exists(ascEntity) ||
                !entityManager.HasBuffer<BEAttrSet>(ascEntity))
            {
                return;
            }

            DynamicBuffer<BEAttrSet> attrSets = entityManager.GetBuffer<BEAttrSet>(ascEntity);
            int attrSetIndex = attrSets.IndexOfAttrSetCode(attrSetCode);
            if (attrSetIndex < 0)
            {
                return;
            }

            BEAttrSet attrSet = attrSets[attrSetIndex];
            int attrIndex = attrSet.Attributes.IndexOfAttrCode(attributeCode);
            if (attrIndex < 0)
            {
                return;
            }

            CAttributeData attributeData = attrSet.Attributes[attrIndex];
            float oldValue = attributeData.CurrentValue;
            attributeData.CurrentValue = currentValue;
            attributeData.Dirty = false;
            attrSet.Attributes[attrIndex] = attributeData;
            attrSets[attrSetIndex] = attrSet;

            GASEventCenter.InvokeOnCurrentValueChangeAfter(
                ascEntity,
                attrSetCode,
                attributeCode,
                oldValue,
                currentValue);
        }
    }
}
