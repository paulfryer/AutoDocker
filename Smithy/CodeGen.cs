using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;

partial class Program
{
    static void YMain()
    {
        // Create an assembly and module
        AssemblyName assemblyName = new AssemblyName("DynamicAssembly");
        AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("DynamicModule");

        // Define a new type
        TypeBuilder typeBuilder = moduleBuilder.DefineType(
            "DynamicClass",
            TypeAttributes.Public | TypeAttributes.Class);

        // Define a field in the class
        FieldBuilder fieldBuilder = typeBuilder.DefineField(
            "dynamicField",
            typeof(string),
            FieldAttributes.Private);

        // Define a constructor
        ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new Type[0]);

        ILGenerator constructorIL = constructorBuilder.GetILGenerator();
        constructorIL.Emit(OpCodes.Ldarg_0);
        constructorIL.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
        constructorIL.Emit(OpCodes.Ret);

        // Define a property
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
            "DynamicProperty",
            PropertyAttributes.None,
            typeof(string),
            null);

        MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
            "get_DynamicProperty",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(string),
            Type.EmptyTypes);

        ILGenerator getMethodIL = getMethodBuilder.GetILGenerator();
        getMethodIL.Emit(OpCodes.Ldarg_0);
        getMethodIL.Emit(OpCodes.Ldfld, fieldBuilder);
        getMethodIL.Emit(OpCodes.Ret);

        propertyBuilder.SetGetMethod(getMethodBuilder);

        // Create the type
        Type dynamicType = typeBuilder.CreateType();

        // Create an instance of the dynamic class
        object dynamicObject = Activator.CreateInstance(dynamicType);

        // Set the field value
        FieldInfo fieldInfo = dynamicType.GetField("dynamicField", BindingFlags.NonPublic | BindingFlags.Instance);
        fieldInfo.SetValue(dynamicObject, "Hello, Dynamic World!");

        // Get the property value
        PropertyInfo propertyInfo = dynamicType.GetProperty("DynamicProperty");
        string dynamicPropertyValue = (string)propertyInfo.GetValue(dynamicObject);

        Console.WriteLine(dynamicPropertyValue);

        // You can use the dynamicType to create more instances or interact with the dynamically generated class.

        // Clean up by saving the assembly if needed
         //assemblyBuilder.Save("DynamicAssembly.dll");



        Console.ReadKey();
    }
    
}
