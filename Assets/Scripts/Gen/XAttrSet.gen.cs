///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using System.Collections.Generic;

namespace GAS.Runtime
{
    public static class XAttrSet
    {
        public const int FightUnit = 1;
        public const int Bullet = 2;


        public class AS_FightUnit
        {
            public const int Health = 1;
            public const int Mana = 2;
            public const int MoveSpeed = 3;
            public const int Attack = 4;
            public const int Defense = 5;
            public const int Stamina = 6;
            public const int MaxHealth = 7;
            public const int MaxMana = 8;
            public const int MaxStamina = 9;
            public const int AttackSpeed = 10;
            public const int Accuracy = 11;
            public const int Dodge = 12;
            public const int CriticalChance = 13;
            public const int CriticalMultiplier = 14;
        }

        public class AS_Bullet
        {
            public const int MoveSpeed = 3;
            public const int Attack = 4;
        }

        private static Dictionary<int, AttrSetConfig> _attributeSetMap = new Dictionary<int, AttrSetConfig>();

        public static Dictionary<int, AttrSetConfig> AttributeSetMap
        {
            get
            {
                if (_attributeSetMap.Count == 0)
                {
                    var datas = XLuban.Tables.TbattributeSet.DataList;
                    foreach (var attrSet in datas)
                    {
                        var settings = new AttributeBaseSetting[attrSet.Attribute.Length];
                        for (var i = 0; i < attrSet.Attribute.Length; i++)
                        {
                            var a = attrSet.Attribute[i];
                            settings[i] = new AttributeBaseSetting(a.ID, a.InitValue, a.UseMinValue,a.UseMaxValue, a.MinValue, a.MaxValue);
                        }
                        _attributeSetMap.Add(attrSet.ID,new AttrSetConfig(attrSet.ID,settings));
                    }
                }
                return _attributeSetMap;
            }
        }
    }
}
