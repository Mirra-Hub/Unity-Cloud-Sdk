using System;

namespace MirraCloud.Json
{
    /// <summary>
    /// Задаёт явное имя поля/свойства в JSON.
    /// Используется маппером при чтении, чтобы сопоставлять имя из JSON с членом класса.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class JsonNameAttribute : Attribute
    {
        public string Name { get; }

        public JsonNameAttribute(string name)
        {
            Name = name;
        }
    }
}

