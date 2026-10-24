namespace Verification;

internal static class Check
{
    public static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    public static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new Exception($"Expected {typeof(T).Name}");
    }
}
