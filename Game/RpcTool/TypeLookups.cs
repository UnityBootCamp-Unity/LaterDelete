namespace PacketTool
{
    internal static class TypeLookups
    {
        public static readonly Dictionary<string, Type> TypeByName = new()
        {
            // 정수 타입
            ["byte"] = typeof(byte),           // System.Byte (0 to 255)
            ["sbyte"] = typeof(sbyte),         // System.SByte (-128 to 127)
            ["short"] = typeof(short),         // System.Int16 (-32,768 to 32,767)
            ["ushort"] = typeof(ushort),       // System.UInt16 (0 to 65,535)
            ["int"] = typeof(int),             // System.Int32 (-2,147,483,648 to 2,147,483,647)
            ["uint"] = typeof(uint),           // System.UInt32 (0 to 4,294,967,295)
            ["long"] = typeof(long),           // System.Int64 (-9,223,372,036,854,775,808 to 9,223,372,036,854,775,807)
            ["ulong"] = typeof(ulong),         // System.UInt64 (0 to 18,446,744,073,709,551,615)

            // 부동소수점 타입
            ["float"] = typeof(float),         // System.Single (7 digits precision)
            ["double"] = typeof(double),       // System.Double (15-17 digits precision)
            ["decimal"] = typeof(decimal),     // System.Decimal (28-29 digits precision)

            // 기타 기본 타입
            ["bool"] = typeof(bool),           // System.Boolean (true/false)
            ["char"] = typeof(char),           // System.Char (16-bit Unicode character)
            ["string"] = typeof(string),       // System.String (sequence of characters)
            ["object"] = typeof(object),       // System.Object (base class of all types)

            // 특수 타입
            ["void"] = typeof(void),           // System.Void (메서드 반환 타입용)

            // nullable 변형들 (선택적 - 필요에 따라 추가)
            ["int?"] = typeof(int?),           // System.Nullable<System.Int32>
            ["bool?"] = typeof(bool?),         // System.Nullable<System.Boolean>
            ["byte?"] = typeof(byte?),         // System.Nullable<System.Byte>
            ["sbyte?"] = typeof(sbyte?),       // System.Nullable<System.SByte>
            ["short?"] = typeof(short?),       // System.Nullable<System.Int16>
            ["ushort?"] = typeof(ushort?),     // System.Nullable<System.UInt16>
            ["uint?"] = typeof(uint?),         // System.Nullable<System.UInt32>
            ["long?"] = typeof(long?),         // System.Nullable<System.Int64>
            ["ulong?"] = typeof(ulong?),       // System.Nullable<System.UInt64>
            ["float?"] = typeof(float?),       // System.Nullable<System.Single>
            ["double?"] = typeof(double?),     // System.Nullable<System.Double>
            ["decimal?"] = typeof(decimal?),   // System.Nullable<System.Decimal>
            ["char?"] = typeof(char?),         // System.Nullable<System.Char>

            // 일반적인 시스템 타입들 (별칭은 아니지만 자주 사용됨)
            ["DateTime"] = typeof(DateTime),           // System.DateTime
            ["TimeSpan"] = typeof(TimeSpan),           // System.TimeSpan
            ["Guid"] = typeof(Guid),                   // System.Guid
            ["IntPtr"] = typeof(IntPtr),               // System.IntPtr
            ["UIntPtr"] = typeof(UIntPtr),             // System.UIntPtr

            // 배열 타입들 (기본적인 것들)
            ["byte[]"] = typeof(byte[]),
            ["int[]"] = typeof(int[]),
            ["string[]"] = typeof(string[]),
            ["bool[]"] = typeof(bool[]),
            ["char[]"] = typeof(char[]),
            ["float[]"] = typeof(float[]),
            ["double[]"] = typeof(double[]),
            ["long[]"] = typeof(long[]),
            ["short[]"] = typeof(short[]),
            ["object[]"] = typeof(object[]),
        };

        public static readonly Dictionary<Type, string> NameByType = new()
        {
            // 정수 타입
            [typeof(byte)] = "byte",
            [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short",
            [typeof(ushort)] = "ushort",
            [typeof(int)] = "int",
            [typeof(uint)] = "uint",
            [typeof(long)] = "long",
            [typeof(ulong)] = "ulong",

            // 부동소수점 타입
            [typeof(float)] = "float",
            [typeof(double)] = "double",
            [typeof(decimal)] = "decimal",

            // 기타 기본 타입
            [typeof(bool)] = "bool",
            [typeof(char)] = "char",
            [typeof(string)] = "string",
            [typeof(object)] = "object",

            // 특수 타입
            [typeof(void)] = "void",

            // nullable 변형들
            [typeof(int?)] = "int?",
            [typeof(bool?)] = "bool?",
            [typeof(byte?)] = "byte?",
            [typeof(sbyte?)] = "sbyte?",
            [typeof(short?)] = "short?",
            [typeof(ushort?)] = "ushort?",
            [typeof(uint?)] = "uint?",
            [typeof(long?)] = "long?",
            [typeof(ulong?)] = "ulong?",
            [typeof(float?)] = "float?",
            [typeof(double?)] = "double?",
            [typeof(decimal?)] = "decimal?",
            [typeof(char?)] = "char?",

            // 자주 쓰이는 시스템 타입
            [typeof(DateTime)] = "DateTime",
            [typeof(TimeSpan)] = "TimeSpan",
            [typeof(Guid)] = "Guid",
            [typeof(IntPtr)] = "IntPtr",
            [typeof(UIntPtr)] = "UIntPtr",

            // 배열 타입
            [typeof(byte[])] = "byte[]",
            [typeof(int[])] = "int[]",
            [typeof(string[])] = "string[]",
            [typeof(bool[])] = "bool[]",
            [typeof(char[])] = "char[]",
            [typeof(float[])] = "float[]",
            [typeof(double[])] = "double[]",
            [typeof(long[])] = "long[]",
            [typeof(short[])] = "short[]",
            [typeof(object[])] = "object[]",
        };
    }
}
