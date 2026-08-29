namespace InventoryAPI.Domain;

internal static class MasterNameMatcher
{
    private const int MinimumMatchLength = 2;

    public static bool IsPartialMatch(string candidate, string input)
    {
        if (candidate.Length < MinimumMatchLength || input.Length < MinimumMatchLength)
        {
            return false;
        }

        for (var index = 0; index <= input.Length - MinimumMatchLength; index++)
        {
            var fragment = input.Substring(index, MinimumMatchLength);
            if (candidate.Contains(fragment, StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
