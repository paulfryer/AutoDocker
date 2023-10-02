using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal partial class Program
{
    private static void GenerateCode(Smithy smithy)
    {
        // Create a compilation unit
        var root = SyntaxFactory.CompilationUnit();

        // Add using statements
        root = root.AddUsings(
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks"))); // Add using for System.Threading.Tasks

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
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)); // Add a semicolon to indicate no method body

                // Add the method to the interface
                interfaceDeclaration = interfaceDeclaration.AddMembers(methodDeclaration);


                // Create the GetWeatherInput class
                ClassDeclarationSyntax getWeatherInputClass = SyntaxFactory.ClassDeclaration(operation.Input.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));

                // Create the GetWeatherOutput class
                ClassDeclarationSyntax getWeatherOutputClass = SyntaxFactory.ClassDeclaration(operation.Output.Name)
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));


                // Add the interface and classes to the namespace
                namespaceDeclaration = namespaceDeclaration.AddMembers(interfaceDeclaration, getWeatherInputClass, getWeatherOutputClass);

            }
            
            // Add the namespace to the compilation unit
            root = root.AddMembers(namespaceDeclaration);
        }


        foreach (var service in smithy.Services)
        {
            // Create a compilation unit (the top-level container for C# code)
            var compilationUnit = SyntaxFactory.CompilationUnit()
                .WithUsings(
                    new SyntaxList<UsingDirectiveSyntax>
                        { SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")) })
                .WithMembers(
                    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                        // Create a namespace
                        SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(service.Namespace))
                            .WithMembers(
                                SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                                    // Create a class
                                    SyntaxFactory.ClassDeclaration(service.Name)
                                        .WithModifiers(
                                            SyntaxFactory.TokenList(
                                                SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                        .WithMembers(
                                            SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                                                // Create a method
                                                SyntaxFactory.MethodDeclaration(
                                                        SyntaxFactory.ParseTypeName("void"), "MyMethod")
                                                    .WithModifiers(
                                                        SyntaxFactory.TokenList(
                                                            SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                                    .WithBody(
                                                        SyntaxFactory.Block(
                                                            SyntaxFactory.ParseStatement(
                                                                "Console.WriteLine(\"Hello, Roslyn!\");")))))))));

            // Convert the compilation unit to a string
            var generatedCode = root.NormalizeWhitespace().ToFullString();

            Console.WriteLine(generatedCode);
        }


        // You can now use the generated code in your application or save it to a file.
    }
}