using System.Text.Json;
using System.Text.Json.Serialization;

namespace Checkers.Web.Infrastructure
{
    /// <summary>
    /// Конвертер для обработки масмвов двумерных[,] в рваные[][]
    /// </summary>
    public class Json2DArrayConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert.IsArray && typeToConvert.GetArrayRank() == 2;
        }

        public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            Type elementType = typeToConvert.GetElementType()!;
            var converterType = typeof(Inner2DArrayConverter<>).MakeGenericType(elementType);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private class Inner2DArrayConverter<T> : JsonConverter<T[,]>
        {
            public override T[,]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => throw new NotImplementedException();

            public override void Write(Utf8JsonWriter writer, T[,] array, JsonSerializerOptions options)
            {
                int rowsCount = array.GetLength(0);
                int colsCount = array.GetLength(1);

                writer.WriteStartArray(); // [
                for (int r = 0; r < rowsCount; r++)
                {
                    writer.WriteStartArray(); // [
                    for (int c = 0; c < colsCount; c++)
                    {
                        JsonSerializer.Serialize(writer, array[r, c], options);
                    }
                    writer.WriteEndArray(); // ]
                }
                writer.WriteEndArray(); // ]
            }
        }
    }
}
