using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

// Compiled against 3.2 dedicated Managed. 3.3 net packages are emitted at
// runtime so GetLength is never required on 3.3.
public static class HSGameApi
{
    static readonly Dictionary<string, Type> emitted = new Dictionary<string, Type>();
    static ModuleBuilder emitMod;

    public static Type NetPackageType33(string packageName, Type coreWithoutGetLength)
    {
        Type existing;
        if (emitted.TryGetValue(packageName, out existing))
            return existing;
        var t = EmitConcrete(packageName, coreWithoutGetLength);
        emitted[packageName] = t;
        return t;
    }

    static Type EmitConcrete(string name, Type core)
    {
        if (emitMod == null)
        {
            var an = new AssemblyName("HSWorkingStuffNet33");
            var asm = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            emitMod = asm.DefineDynamicModule("HSWorkingStuffNet33");
        }
        var tb = emitMod.DefineType(name, TypeAttributes.Public | TypeAttributes.Sealed, core);
        var ctor = tb.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, Type.EmptyTypes);
        var il = ctor.GetILGenerator();
        var baseCtor = core.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (baseCtor == null)
            baseCtor = typeof(NetPackage).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
        return tb.CreateType();
    }

    public static NetPackage GetNetPackage(Type packageType)
    {
        var methods = typeof(NetPackageManager).GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            var mi = methods[i];
            if (mi.Name != "GetPackage" || !mi.IsGenericMethodDefinition)
                continue;
            if (mi.GetParameters().Length != 0)
                continue;
            return (NetPackage)mi.MakeGenericMethod(packageType).Invoke(null, null);
        }
        return (NetPackage)Activator.CreateInstance(packageType);
    }
}
