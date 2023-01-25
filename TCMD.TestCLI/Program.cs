
using TCMD;

Command.Add("test", (int a, bool g3, string t) =>
{
    Console.WriteLine($"This is the test.. \n 'a' having the value of {a}\n 'g3' having the value of {g3}\n 't' having the value of {t}..");
});

await Command.ParseArguments();

public static class YAY
{
    [CMD]
    public static void Add(int a, int b = 2)
    {
        Console.WriteLine($"result: {a + b}");
    }

    [CMD]
    public static void Div(double a, double b)
    {
        Console.WriteLine($"result: {a / b}");
    }
}