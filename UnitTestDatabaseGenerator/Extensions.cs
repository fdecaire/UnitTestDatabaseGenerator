namespace UnitTestDatabaseGenerator
{
    public static class Extensions
    {
        public static string FixSpecialCharacters(this string s)
        {
            var result = s.Replace("-", "_");
            result = result.Replace(" ", "_");
            result = result.Replace(".", "_");

            return result;
        }
    }
}
