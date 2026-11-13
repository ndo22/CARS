// Najde všechny metody s [Test] a spustí je. Návratový kód 1 = některý test selhal.
using System;
using System.Linq;
using System.Reflection;

public static class TestRunner
{
    public static int Main()
    {
        int passed = 0, failed = 0;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().OrderBy(t => t.Name))
        {
            foreach (MethodInfo m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try
                {
                    m.Invoke(Activator.CreateInstance(type), null);
                    passed++;
                    Console.WriteLine($"  PASS  {type.Name}.{m.Name}");
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine($"  FAIL  {type.Name}.{m.Name}: {e.InnerException?.Message}");
                }
            }
        }
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
