using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal partial class Program
{
    private static void GenerateCode(Smithy smithy)
    {
        // Create a compilation unit
        var root = SyntaxFactory.CompilationUnit();

        // Add using statements
        root = root.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName("System.Threading.Tasks"))); // Add using for System.Threading.Tasks

        foreach (var service in smithy.Services)
        {
            // Create the namespace
            var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(service.Namespace));

            // Create the interface
            var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration($"I{service.Name}Service")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));


            foreach (var operation in service.Operations)
            {
                // Create the method declaration
                var methodDeclaration = SyntaxFactory.MethodDeclaration(
                        SyntaxFactory.GenericName(SyntaxFactory.Identifier("Task"))
                            .WithTypeArgumentList(SyntaxFactory.TypeArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.ParseTypeName(operation.Output.Name)))),
                        operation.Name)
                    .WithParameterList(
                        SyntaxFactory.ParameterList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Parameter(
                                        SyntaxFactory.Identifier("input"))
                                    .WithType(SyntaxFactory.ParseTypeName(operation.Input.Name)))))
                    .WithSemicolonToken(
                        SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Add a semicolon to indicate no method body

                // Add the method to the interface
                interfaceDeclaration = interfaceDeclaration.AddMembers(methodDeclaration);


                // Create the input class
                var inputClass = SyntaxFactory.ClassDeclaration(operation.Input.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var inputMember in operation.Input.Members)
                    inputClass = inputClass.AddMembers(
                        SyntaxFactory.PropertyDeclaration(
                                SyntaxFactory.ParseTypeName(inputMember.Value.Name), inputMember.Key)
                            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                            .WithAccessorList(
                                SyntaxFactory.AccessorList(
                                    SyntaxFactory.SingletonList(
                                        SyntaxFactory.AccessorDeclaration(
                                                SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
                                )
                            ));

                // output class
                var outputClass = SyntaxFactory.ClassDeclaration(operation.Output.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                foreach (var outputMember in operation.Output.Members)
                    outputClass = outputClass.AddMembers(
                    SyntaxFactory.PropertyDeclaration(
                            SyntaxFactory.ParseTypeName(outputMember.Value.Name), outputMember.Key)
                        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                        .WithAccessorList(
                            SyntaxFactory.AccessorList(
                                SyntaxFactory.SingletonList(
                                    SyntaxFactory.AccessorDeclaration(
                                            SyntaxKind.GetAccessorDeclaration)
                                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)))
                            )
                        ));

                // Add the interface and classes to the namespace
                namespaceDeclaration = namespaceDeclaration.AddMembers(inputClass,
                    outputClass);
            }

            namespaceDeclaration = namespaceDeclaration.AddMembers(interfaceDeclaration);
            // Add the namespace to the compilation unit
            root = root.AddMembers(namespaceDeclaration);
        }


        // Convert the compilation unit to a string
        var generatedCode = root.NormalizeWhitespace().ToFullString();

        Console.WriteLine(generatedCode);
    }


    // You can now use the generated code in your application or save it to a file.
}