namespace MirraCloud.Core.Enums
{
    public static class LanguageCodeExtensions
    {
        public static string ToLanguageString(this LanguageCode languageCode)
        {
            return languageCode.ToString("G");
        }
    }
}
