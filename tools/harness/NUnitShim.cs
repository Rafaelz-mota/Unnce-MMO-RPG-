// Mini-implementação do subconjunto de NUnit usado pelos testes do projeto.
// SÓ para rodar os testes fora do Unity (tools/harness). Nunca entra no cliente:
// o Unity usa o NUnit oficial do Unity Test Framework com a mesma sintaxe.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class OneTimeSetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public sealed class CategoryAttribute : Attribute { public CategoryAttribute(string name) { } }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class TestCaseAttribute : Attribute
    {
        public object[] Arguments { get; private set; }
        public TestCaseAttribute(params object[] arguments) { Arguments = arguments; }
    }

    public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }

    public delegate void TestDelegate();

    public static class Assert
    {
        private static string Fmt(string message) { return string.IsNullOrEmpty(message) ? "" : " - " + message; }

        private static bool IsNumeric(object o)
        {
            return o is sbyte || o is byte || o is short || o is ushort || o is int || o is uint || o is long || o is ulong || o is float || o is double || o is decimal;
        }

        private static bool EqualObjects(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (IsNumeric(a) && IsNumeric(b)) return Convert.ToDecimal(a) == Convert.ToDecimal(b);
            if (a is IEnumerable && b is IEnumerable && !(a is string))
            {
                var la = ((IEnumerable)a).Cast<object>().ToList();
                var lb = ((IEnumerable)b).Cast<object>().ToList();
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!EqualObjects(la[i], lb[i])) return false;
                return true;
            }
            return a.Equals(b);
        }

        public static void Fail(string message) { throw new AssertionException("Fail" + Fmt(message)); }
        public static void Pass() { }

        public static void IsTrue(bool condition, string message = null)
        {
            if (!condition) throw new AssertionException("Esperado True" + Fmt(message));
        }

        public static void IsFalse(bool condition, string message = null)
        {
            if (condition) throw new AssertionException("Esperado False" + Fmt(message));
        }

        public static void IsNull(object o, string message = null)
        {
            if (o != null) throw new AssertionException("Esperado null, veio " + o + Fmt(message));
        }

        public static void IsNotNull(object o, string message = null)
        {
            if (o == null) throw new AssertionException("Esperado não-nulo" + Fmt(message));
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!EqualObjects(expected, actual))
                throw new AssertionException("Esperado <" + expected + "> mas veio <" + actual + ">" + Fmt(message));
        }

        public static void AreEqual(double expected, double actual, double delta, string message = null)
        {
            if (double.IsNaN(expected) || double.IsNaN(actual) || Math.Abs(expected - actual) > delta)
                throw new AssertionException("Esperado <" + expected + "> ±" + delta + " mas veio <" + actual + ">" + Fmt(message));
        }

        public static void AreNotEqual(object expected, object actual, string message = null)
        {
            if (EqualObjects(expected, actual)) throw new AssertionException("Esperado diferente de <" + expected + ">" + Fmt(message));
        }

        public static void AreSame(object expected, object actual, string message = null)
        {
            if (!ReferenceEquals(expected, actual)) throw new AssertionException("Esperado a mesma instância" + Fmt(message));
        }

        public static void Greater(double a, double b, string message = null)
        {
            if (!(a > b)) throw new AssertionException("Esperado " + a + " > " + b + Fmt(message));
        }

        public static void GreaterOrEqual(double a, double b, string message = null)
        {
            if (!(a >= b)) throw new AssertionException("Esperado " + a + " >= " + b + Fmt(message));
        }

        public static void Less(double a, double b, string message = null)
        {
            if (!(a < b)) throw new AssertionException("Esperado " + a + " < " + b + Fmt(message));
        }

        public static void LessOrEqual(double a, double b, string message = null)
        {
            if (!(a <= b)) throw new AssertionException("Esperado " + a + " <= " + b + Fmt(message));
        }

        public static T Throws<T>(TestDelegate code, string message = null) where T : Exception
        {
            try { code(); }
            catch (Exception ex)
            {
                if (ex is T) return (T)ex;
                throw new AssertionException("Esperado " + typeof(T).Name + " mas veio " + ex.GetType().Name + ": " + ex.Message + Fmt(message));
            }
            throw new AssertionException("Esperado " + typeof(T).Name + " mas nada foi lançado" + Fmt(message));
        }
    }

    public static class CollectionAssert
    {
        public static void Contains(IEnumerable collection, object item, string message = null)
        {
            foreach (object o in collection) if (Equals(o, item)) return;
            throw new AssertionException("Coleção não contém <" + item + ">" + (message == null ? "" : " - " + message));
        }

        public static void DoesNotContain(IEnumerable collection, object item, string message = null)
        {
            foreach (object o in collection)
                if (Equals(o, item)) throw new AssertionException("Coleção contém <" + item + ">" + (message == null ? "" : " - " + message));
        }
    }
}

namespace MiniNUnit
{
    public static class Runner
    {
        /// <summary>Roda todos os [TestFixture] de um assembly. Retorna o número de falhas.</summary>
        public static int Run(Assembly asm, string nameFilter)
        {
            int passed = 0, failed = 0;
            var failures = new List<string>();
            var total = Stopwatch.StartNew();
            foreach (Type type in asm.GetTypes().Where(t => t.GetCustomAttribute<NUnit.Framework.TestFixtureAttribute>() != null).OrderBy(t => t.Name))
            {
                if (!string.IsNullOrEmpty(nameFilter) && type.FullName.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                object fixture = Activator.CreateInstance(type);
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                MethodInfo oneTime = methods.FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.OneTimeSetUpAttribute>() != null);
                MethodInfo setUp = methods.FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);
                MethodInfo tearDown = methods.FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.TearDownAttribute>() != null);
                Console.WriteLine("== " + type.Name);
                try { if (oneTime != null) oneTime.Invoke(fixture, null); }
                catch (TargetInvocationException ex)
                {
                    failed++;
                    failures.Add(type.Name + ".OneTimeSetUp: " + ex.InnerException);
                    Console.WriteLine("  [FALHA] OneTimeSetUp: " + ex.InnerException.Message);
                    continue;
                }
                foreach (MethodInfo m in methods.OrderBy(x => x.Name))
                {
                    var cases = m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().ToList();
                    bool isTest = m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null;
                    if (!isTest && cases.Count == 0) continue;
                    var argSets = new List<object[]>();
                    if (cases.Count == 0) argSets.Add(null);
                    else foreach (var c in cases) argSets.Add(c.Arguments);
                    foreach (object[] args in argSets)
                    {
                        string label = type.Name + "." + m.Name + (args == null ? "" : "(" + string.Join(", ", args.Select(a => Convert.ToString(a))) + ")");
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            if (setUp != null) setUp.Invoke(fixture, null);
                            try { m.Invoke(fixture, args); }
                            finally { if (tearDown != null) tearDown.Invoke(fixture, null); }
                            passed++;
                            Console.WriteLine("  [ok]    " + label + " (" + sw.ElapsedMilliseconds + " ms)");
                        }
                        catch (TargetInvocationException ex)
                        {
                            failed++;
                            Exception inner = ex.InnerException;
                            string first = inner.Message.Split('\n')[0];
                            failures.Add(label + ": " + inner.GetType().Name + ": " + inner.Message + (inner is NUnit.Framework.AssertionException ? "" : "\n" + inner.StackTrace));
                            Console.WriteLine("  [FALHA] " + label + " -> " + first);
                        }
                    }
                }
            }
            Console.WriteLine();
            foreach (string f in failures) Console.WriteLine("FALHA: " + f);
            Console.WriteLine("Resultado: " + passed + " passaram, " + failed + " falharam (" + total.ElapsedMilliseconds + " ms)");
            return failed;
        }
    }
}
