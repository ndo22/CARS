// Minimální náhrada NUnit pro spuštění testů v CI bez Unity (stejné API, jaké testy používají).
using System;
namespace NUnit.Framework
{
    public class TestAttribute : Attribute { }
    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
    public static class Assert
    {
        static void Fail(string what, string msg) => throw new AssertionException(what + (string.IsNullOrEmpty(msg) ? "" : " - " + msg));
        public static void IsTrue(bool c, string msg = null) { if (!c) Fail("expected true", msg); }
        public static void IsFalse(bool c, string msg = null) { if (c) Fail("expected false", msg); }
        public static void IsNotNull(object o, string msg = null) { if (o == null) Fail("expected not null", msg); }
        public static void AreEqual(object e, object a, string msg = null) { if (!Equals(e, a)) Fail($"expected <{e}> but was <{a}>", msg); }
        public static void AreNotEqual(object e, object a, string msg = null) { if (Equals(e, a)) Fail($"expected not <{e}>", msg); }
        public static void AreEqual(float e, float a, float d, string msg = null) { if (Math.Abs(e - a) > d) Fail($"expected {e} +- {d} but was {a}", msg); }
        public static void Greater(float a, float b, string msg = null) { if (!(a > b)) Fail($"expected {a} > {b}", msg); }
        public static void Less(float a, float b, string msg = null) { if (!(a < b)) Fail($"expected {a} < {b}", msg); }
        public static void GreaterOrEqual(float a, float b, string msg = null) { if (!(a >= b)) Fail($"expected {a} >= {b}", msg); }
        public static void LessOrEqual(float a, float b, string msg = null) { if (!(a <= b)) Fail($"expected {a} <= {b}", msg); }
    }
}
