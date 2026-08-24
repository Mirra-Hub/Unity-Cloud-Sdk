namespace MirraCloud.Core.Enums
{
    public static class CountryCodeExtensions
    {
        public static string ToCountryString(this CountryCode countryCode)
        {
            return countryCode.ToString("G");
        }
    }
}
