using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Moq;

namespace Decompiler
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Set up dependency injection
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddTransient<ITestScriptGenerator, TestScriptGenerator>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var testScriptGenerator = serviceProvider.GetRequiredService<ITestScriptGenerator>();

            // Define the output directory for the test scripts
            var outputDirectory = Path.Combine(Directory.GetCurrentDirectory(), "GeneratedTests");

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Generate test scripts
            testScriptGenerator.GenerateTestScripts(outputDirectory);

            Console.WriteLine($"Test scripts generated in: {outputDirectory}");
        }
    }

    public interface ITestScriptGenerator
    {
        void GenerateTestScripts(string outputDirectory);
    }

    public class TestScriptGenerator : ITestScriptGenerator
    {
        private readonly IServiceProvider _serviceProvider;

        public TestScriptGenerator(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void GenerateTestScripts(string outputDirectory)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract))
                {
                    GenerateTestForType(type, outputDirectory);
                }
            }
        }

        private void GenerateTestForType(Type type, string outputDirectory)
        {
            var className = type.Name;
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            if (!methods.Any()) return;

            var testClassName = $"{className}Tests";
            var testFileName = Path.Combine(outputDirectory, $"{testClassName}.cs");

            using (var writer = new StreamWriter(testFileName))
            {
                writer.WriteLine("using Xunit;");
                writer.WriteLine("using Moq;");
                writer.WriteLine($"public class {testClassName}");
                writer.WriteLine("{");

                GenerateConstructorAndMocks(writer, type);

                foreach (var method in methods)
                {
                    GenerateTestForMethod(writer, className, method);
                }

                writer.WriteLine("}");
            }
        }

        private void GenerateConstructorAndMocks(StreamWriter writer, Type type)
        {
            var constructor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (constructor == null) return;

            var parameters = constructor.GetParameters();

            // Declare the mock fields
            foreach (var parameter in parameters)
            {
                writer.WriteLine($"\tprivate readonly Mock<{parameter.ParameterType.Name}> _{parameter.Name}Mock;");
            }
            writer.WriteLine();

            // Generate the constructor for the test class
            writer.WriteLine($"\tpublic {type.Name}Tests()");
            writer.WriteLine("\t{");

            // Instantiate mocks
            foreach (var parameter in parameters)
            {
                writer.WriteLine($"\t\t_{parameter.Name}Mock = new Mock<{parameter.ParameterType.Name}>();");
            }

            writer.WriteLine("\t}");
        }

        private void GenerateTestForMethod(StreamWriter writer, string className, MethodInfo method)
        {
            var methodName = method.Name;
            var parameters = method.GetParameters();

            writer.WriteLine($"\t[Fact]");
            writer.WriteLine($"\tpublic void {methodName}_Test()");
            writer.WriteLine("\t{");

            // Instantiate the class under test with mocks
            var constructor = method.DeclaringType.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (constructor != null)
            {
                var constructorArgs = string.Join(", ", constructor.GetParameters().Select(p => $"_{p.Name}Mock.Object"));
                writer.WriteLine($"\t\tvar instance = new {className}({constructorArgs});");
            }
            else
            {
                writer.WriteLine($"\t\tvar instance = new {className}();");
            }

            var parameterValues = string.Join(", ", parameters.Select(p => GetDefaultValue(p.ParameterType)));
            writer.WriteLine($"\t\tvar result = instance.{methodName}({parameterValues});");

            writer.WriteLine("\t\t// Assert here");
            writer.WriteLine("\t}");
        }

        private string GetDefaultValue(Type type)
        {
            if (type == typeof(int)) return "0";
            if (type == typeof(string)) return "\"\"";
            if (type == typeof(bool)) return "false";
            // Add more default values for common types as needed

            return "null";
        }
    }


}