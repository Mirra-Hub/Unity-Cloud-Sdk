using System;

namespace MirraCloud.Json
{
    /// <summary>
    /// Указывает, что имя поля/свойства в JSON должно быть в camelCase.
    /// Например, поле PlayerInfo будет сериализовано/десериализовано как playerInfo.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonNameCamelAttribute : Attribute
    {
    }
}

