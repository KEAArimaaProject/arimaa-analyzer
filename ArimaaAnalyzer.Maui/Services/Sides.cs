
    using System;
    using System.Text.RegularExpressions;

// 1. Attribute must be defined BEFORE it's used
    [AttributeUsage(AttributeTargets.Field)]
    public class SidesCodeAttribute : Attribute
    {
        public string Code { get; }

        public SidesCodeAttribute(string code)
        {
            Code = code;
        }
    }

// 2. Now the enum can safely use the attribute
    public enum Sides
    {
        [SidesCode("g")] Gold,

        [SidesCode("s")] Silver
    }

// 3. Your extension (you can rename the class later if you want)
    public static class SidesExtensions // renamed from MetalExtensions for clarity
    {
        public static string GetCode(this Sides side)
        {
            var field = side.GetType().GetField(side.ToString());
            var attribute = (SidesCodeAttribute)Attribute.GetCustomAttribute(field, typeof(SidesCodeAttribute));
            return attribute?.Code ?? side.ToString();
        }
    }
