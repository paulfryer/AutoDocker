using System.Reflection;
using System.Reflection.Emit;

internal partial class Program
{
    private static void YMain()
    {
        // Create an assembly and module
        var assemblyName = new AssemblyName("DynamicAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");

        // Define a new type
        var typeBuilder = moduleBuilder.DefineType(
            "DynamicClass",
            TypeAttributes.Public | TypeAttributes.Class);

        // Define a field in the class
        var fieldBuilder = typeBuilder.DefineField(
            "dynamicField",
            typeof(string),
            FieldAttributes.Private);

        // Define a constructor
        var constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new Type[0]);

        var constructorIL = constructorBuilder.GetILGenerator();
        constructorIL.Emit(OpCodes.Ldarg_0);
        constructorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
        constructorIL.Emit(OpCodes.Ret);

        // Define a property
        var propertyBuilder = typeBuilder.DefineProperty(
            "DynamicProperty",
            PropertyAttributes.None,
            typeof(string),
            null);

        var getMethodBuilder = typeBuilder.DefineMethod(
            "get_DynamicProperty",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string),
            Type.EmptyTypes);

        var getMethodIL = getMethodBuilder.GetILGenerator();
        getMethodIL.Emit(OpCodes.Ldarg_0);
        getMethodIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getMethodIL.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getMethodBuilder);

        // Create the type
        var dynamicType = typeBuilder.CreateType();

        // Create an instance of the dynamic class
        var dynamicObject = Activator.CreateInstance(dynamicType);

        // Set the field value
        var fieldInfo = dynamicType.GetField("dynamicField", BindingFlags.NonPublic | BindingFlags.Instance);
        fieldInfo.SetValue(dynamicObject, "Hello, Dynamic World!");

        // Get the property value
        var propertyInfo = dynamicType.GetProperty("DynamicProperty");
        var dynamicPropertyValue = (string)propertyInfo.GetValue(dynamicObject);

        Console.WriteLine(dynamicPropertyValue);

        // You can use the dynamicType to create more instances or interact with the dynamically generated class.

        // Clean up by saving the assembly if needed
        //assemblyBuilder.Save("DynamicAssembly.dll");


        Console.ReadKey();
    }
}