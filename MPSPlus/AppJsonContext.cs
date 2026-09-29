using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace MPSPlus
{
    [JsonSerializable(typeof(MyClass))]
    [JsonSerializable(typeof(MyClass2))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }


    public class MyClass2
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            RespectNullableAnnotations = true,
            TypeInfoResolver = new OptionalAwareResolver(AppJsonContext.Default),
            Converters = { new OptionalConverter<string?>(), new OptionalNullableConverter<string?>() },
        };
        private static readonly JsonTypeInfo<MyClass> MyClassInfo = (JsonTypeInfo<MyClass>)Options.GetTypeInfo(typeof(MyClass));

        public string? Key { get; set; }

        void DDD()
        {
            var jsonWithExplicitNull = @"{
  ""Key"": ""asd"",
  ""Name"": ""123asd"",
  ""Description"": null
}
";

            var jsonWithMissing = @"{
            ""Key"": ""asd"",
            ""Name"": ""123asd""
        }";

            var jsonWithValue = @"{
  ""Key"": ""asd"",
  ""Name"": ""123asd"",
  ""Description"": ""hello""
}
";

            var withNull = JsonSerializer.Deserialize(jsonWithExplicitNull, MyClassInfo);
            var missing = JsonSerializer.Deserialize(jsonWithMissing, MyClassInfo);
            var withValue = JsonSerializer.Deserialize(jsonWithValue, MyClassInfo);
            var asw = JsonSerializer.Deserialize(jsonWithValue, AppJsonContext.Default.MyClass2);
            Console.WriteLine(withNull is null ? "(null)" : JsonSerializer.Serialize(withNull, MyClassInfo));
            Console.WriteLine(missing is null ? "(null)" : JsonSerializer.Serialize(missing, MyClassInfo));
            Console.WriteLine(withValue is null ? "(null)" : JsonSerializer.Serialize(withValue, MyClassInfo));
        }
    }

    public class MyClass
    {
        public string? Key { get; set; }
        public string? Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Optional<string>? Description { get; set; }

        public override string ToString()
        {
            return $"Key: {Key}, Name: {Name}, Description: {Description?.Value}";
        }
    }

    /// <summary>非泛型接口，让 ShouldSerialize 能不依赖泛型判断 HasValue。</summary>
    public interface IOptional
    {
        bool HasValue { get; }
    }

    /// <summary>
    /// 三态属性类型，用于区分"字段缺失"和"字段为 null"：
    /// 反序列化时，字段缺失 → IsMissing；显式 null 或有值 → HasValue = true。
    /// 序列化时，IsMissing → 整个属性省略；HasValue = true → 原样输出（包括 null）。
    ///
    /// 两种用法：
    ///  - 常规：Optional&lt;T&gt; + OptionalAwareResolver —— 省略经 ShouldSerialize 委托，装箱一次
    ///  - 高性能：Optional&lt;T&gt;? + 属性标 [JsonIgnore(Condition = WhenWritingNull)]
    ///           —— 缺失 = 属性为 null，靠忽略条件省略，零装箱
    /// </summary>
    public readonly record struct Optional<T>(bool HasValue, T? Value) : IOptional
    {
        public bool IsMissing => !HasValue;

        public static implicit operator Optional<T>(T? value) => new(true, value);
        public static implicit operator T?(Optional<T> optional) => optional.Value;
    }

    public sealed class OptionalConverter<T> : JsonConverter<Optional<T>>
    {
        // 可空风格（Optional<T>?）依赖它：显式 null 时 Read 才会被调用，
        // 否则序列化器直接把属性置 null，无法与"字段缺失"区分
        public override bool HandleNull => true;

        private JsonConverter<T>? _converter;

        // 缓存内层类型的转换器，读写直接调用，避免嵌套进入 JsonSerializer 的入口开销
        private JsonConverter<T> Converter(JsonSerializerOptions options)
            => _converter ??= (JsonConverter<T>)options.GetTypeInfo(typeof(T)).Converter;

        // 字段在源 JSON 中缺失时，Read 根本不会被调用，保持 default/属性 null → IsMissing
        public override Optional<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null && !typeof(T).IsValueType)
            {
                return new(true, default);   // 显式 null：有值但为 null
            }

            return new(true, Converter(options).Read(ref reader, typeof(T), options));
        }

        public override void Write(Utf8JsonWriter writer, Optional<T> value, JsonSerializerOptions options)
        {
            if (value.Value is null)
            {
                writer.WriteNullValue();
                return;
            }

            Converter(options).Write(writer, value.Value, options);
        }
    }

    /// <summary>
    /// 高性能风格（Optional&lt;T&gt;? + [JsonIgnore(Condition = WhenWritingNull)]）用的转换器：
    /// T' 就是可空结构体本身，null token 会进入 Read，从而把"显式 null"变成 HasValue=true。
    /// </summary>
    public sealed class OptionalNullableConverter<T> : JsonConverter<Optional<T>?>
    {
        // 不开这个，null token 会被序列化器短路，"显式 null"和"缺失"就无法区分
        public override bool HandleNull => true;

        private JsonConverter<T>? _inner;

        private JsonConverter<T> Inner(JsonSerializerOptions options)
            => _inner ??= (JsonConverter<T>)options.GetTypeInfo(typeof(T)).Converter;

        public override Optional<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return new Optional<T>(true, default);   // 显式 null：有值但为 null
            }

            return new Optional<T>(true, Inner(options).Read(ref reader, typeof(T), options));
        }

        // 属性为 null（缺失）时由 WhenWritingNull 直接省略，不会进来
        public override void Write(Utf8JsonWriter writer, Optional<T>? value, JsonSerializerOptions options)
        {
            if (value is null || value.Value.Value is null)
            {
                writer.WriteNullValue();
                return;
            }

            Inner(options).Write(writer, value.Value.Value, options);
        }
    }

    /// <summary>
    /// 包装源生成 context，给 Optional&lt;T&gt; 属性自动挂 ShouldSerialize
    /// （HasValue=false → 序列化时省略整个属性）。
    /// </summary>
    public sealed class OptionalAwareResolver(IJsonTypeInfoResolver inner) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            var info = inner.GetTypeInfo(type, options);

            if (info is { Kind: JsonTypeInfoKind.Object })
            {
                var optional = info.Properties.Where(x => x.PropertyType.IsGenericType && x.PropertyType.GetGenericTypeDefinition() == typeof(Optional<>) && x.ShouldSerialize is null).ToList();
                if (optional.Count == 0)
                {
                    return info;
                }
                foreach (var option in optional)
                {
                    option.ShouldSerialize = (_, value) => value is IOptional { HasValue: true };
                }
            }

            return info;
        }
    }

}
