using System;
using Stl;
using Stl.Serialization;

namespace TodoApp.Helpers
{
    public static class JsonValue
    {
        public static JsonBox<TValue> New<TValue>() => new();
        public static JsonBox<TValue> New<TValue>(TValue value) => new(value);
        public static JsonBox<TValue> New<TValue>(string json) => new(json);

        public static JsonBox<TValue> New<TValue>(Func<ISerializer<string>> serializer)
            => new CustomSerializerJsonBox<TValue>(serializer);
        public static JsonBox<TValue> New<TValue>(Func<ISerializer<string>> serializer, TValue value)
            => new CustomSerializerJsonBox<TValue>(serializer, value);
        public static JsonBox<TValue> New<TValue>(Func<ISerializer<string>> serializer, string json)
            => new CustomSerializerJsonBox<TValue>(serializer, json);
    }

    public class JsonBox<TValue>
    {
        private Option<TValue> _valueOption;
        private Option<string> _jsonOption;

        public TValue Value {
            get => _valueOption.ValueOr(Deserialize());
            set {
                _valueOption = value;
                _jsonOption = Option<string>.None;
            }
        }

        public string Json {
            get => _jsonOption.ValueOr(Serialize());
            set {
                _valueOption = Option<TValue>.None;
                _jsonOption = value;
            }
        }

        public JsonBox() { }
        public JsonBox(TValue value) => Value = value;
        public JsonBox(string json) => Json = json;

        private string Serialize()
        {
            if (!_valueOption.IsSome(out var value))
                throw new InvalidOperationException($"{nameof(Value)} isn't set.");
            var jsonValue = !typeof(TValue).IsValueType && ReferenceEquals(value, null)
                ? ""
                : CreateSerializer().Serialize(value);
            _jsonOption = jsonValue;
            return jsonValue;
        }

        private TValue Deserialize()
        {
            if (!_jsonOption.IsSome(out var jsonValue))
                throw new InvalidOperationException($"{nameof(Json)} isn't set.");
            var value = string.IsNullOrEmpty(jsonValue)
                ? default!
                : CreateSerializer().Deserialize<TValue>(jsonValue);
            _valueOption = value;
            return value;
        }

        protected virtual ISerializer<string> CreateSerializer() => new JsonNetSerializer();
    }

    internal class CustomSerializerJsonBox<TValue> : JsonBox<TValue>
    {
        private Func<ISerializer<string>> SerializerFactory { get; set; }

        public CustomSerializerJsonBox(Func<ISerializer<string>> serializerFactory) => SerializerFactory = serializerFactory;
        public CustomSerializerJsonBox(Func<ISerializer<string>> serializerFactory, TValue value) : this(serializerFactory) => Value = value;
        public CustomSerializerJsonBox(Func<ISerializer<string>> serializerFactory, string json) : this(serializerFactory) => Json = json;

        protected override ISerializer<string> CreateSerializer() => SerializerFactory.Invoke();
    }
}
