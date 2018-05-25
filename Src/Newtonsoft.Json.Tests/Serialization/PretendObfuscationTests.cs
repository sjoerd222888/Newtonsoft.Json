using System;
using System.Reflection;
using System.Reflection.Emit;
#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
using NUnit.Framework;

#endif



namespace Newtonsoft.Json.Tests.Serialization
{
    [TestFixture]
    public class PretendObfuscationTests
    {
        public class TestModelClean
        {
            public int a { get; set; }
        }

        public class TestModelObfuscationBase
        {
            [NonSerialized]
#pragma warning disable 169
            string a;
#pragma warning restore 169

        }

        public static Type CompileResultType()
        {
            TypeBuilder tb = GetTypeBuilder();
            FieldBuilder fieldBuilder = tb.DefineField("a", typeof(string), FieldAttributes.Private);
            Type attributeType = typeof(NonSerializedAttribute);
            ConstructorInfo constructorInfo = attributeType.GetConstructor(Type.EmptyTypes);
            Assert.IsTrue(constructorInfo != null);
            var cab = new CustomAttributeBuilder(
                constructorInfo, new object[0],
                new PropertyInfo[0], new object[0]
            );
            fieldBuilder.SetCustomAttribute(cab);
            AddProperty(tb, "a", typeof(int));
#if DNXCORE50
            TypeInfo typeInfo = tb.CreateTypeInfo();
            return typeInfo.AsType();
#else
            Type objectType = tb.CreateType();
            return objectType;
#endif
        }

        private static TypeBuilder GetTypeBuilder()
        {
            string typeSignature = "TestModelWithObfuscation";
            AssemblyName an = new AssemblyName(typeSignature);
#if DNXCORE50
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
#else
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
#endif
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("PretendObfuscationSerializationTest");
            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                TypeAttributes.Public |
                TypeAttributes.Class |
                TypeAttributes.AutoClass |
                TypeAttributes.AnsiClass |
                TypeAttributes.BeforeFieldInit |
                TypeAttributes.AutoLayout,
                typeof(TestModelObfuscationBase));
            return tb;
        }

        static public PropertyBuilder AddProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType)
        {
            FieldBuilder field = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private);
            PropertyBuilder property =
                typeBuilder.DefineProperty(propertyName,
                    PropertyAttributes.None,
                    propertyType,
                    null);
            MethodAttributes GetSetAttr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            MethodBuilder currGetPropMthdBldr =
                typeBuilder.DefineMethod("get_" + propertyName,
                    GetSetAttr,
                    propertyType,
                    Type.EmptyTypes);
            ILGenerator currGetIL = currGetPropMthdBldr.GetILGenerator();
            currGetIL.Emit(OpCodes.Ldarg_0);
            currGetIL.Emit(OpCodes.Ldfld, field);
            currGetIL.Emit(OpCodes.Ret);
            MethodBuilder currSetPropMthdBldr =
                typeBuilder.DefineMethod("set_" + propertyName,
                    GetSetAttr,
                    null,
                    new Type[] {propertyType});
            ILGenerator currSetIL = currSetPropMthdBldr.GetILGenerator();
            currSetIL.Emit(OpCodes.Ldarg_0);
            currSetIL.Emit(OpCodes.Ldarg_1);
            currSetIL.Emit(OpCodes.Stfld, field);
            currSetIL.Emit(OpCodes.Ret);
            property.SetGetMethod(currGetPropMthdBldr);
            property.SetSetMethod(currSetPropMthdBldr);
            return property;
        }

        /// <summary>
        /// This test tries to deserialize to an artifical type that looks similar to what we can get after a obfuscation process.
        /// The dynamicaly created type has the structure:
        /// 
        /// public class TestModelObfuscationBase {
        ///     [NonSerialized]
        ///     string a
        /// }
        /// 
        /// public class TestModelWithObfuscation {
        ///     [NonSerialized]
        ///     new string a
        /// 
        ///     public int { get; set; }
        /// }
        /// 
        /// 
        /// </summary>
        [Test]
        public void DeserializeObfuscatedObject()
        {
            TestModelClean model = new TestModelClean();
            model.a = 3;
            var json = JsonConvert.SerializeObject(model);
            Type dynamicType = CompileResultType();
            // 1. Test wheter we can activate the artifical type
            object modelDeserilized = Activator.CreateInstance(dynamicType, new object[0]);
            Assert.IsNotNull(modelDeserilized);
            // 2. Test wheter we can deserialize this type.
            modelDeserilized = JsonConvert.DeserializeObject(json, dynamicType,  new JsonSerializerSettings()
            {
                MissingMemberHandling = MissingMemberHandling.Error
            });
            Assert.IsNotNull(modelDeserilized);
            PropertyInfo propertyInfoA = modelDeserilized.GetType().GetProperty("a", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(propertyInfoA);
            int valueDeserialized = (int)propertyInfoA.GetValue(modelDeserilized, null);
            Assert.AreEqual(model.a, valueDeserialized);
        }
    }
}