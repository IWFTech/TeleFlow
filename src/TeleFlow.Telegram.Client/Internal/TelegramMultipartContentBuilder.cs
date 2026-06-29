using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Converts a Telegram request payload that contains uploads into multipart fields and files.
/// Nested files are exposed to Telegram JSON fields through attach:// references.
/// This path is used when a user sends local streams/files through generated Bot API methods.
/// </summary>
internal sealed class TelegramMultipartContentBuilder
{
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly List<TelegramMultipartField> _fields = [];
    private readonly List<TelegramMultipartFile> _files = [];
    private int _fileIndex;

    public TelegramMultipartContentBuilder(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
    }

    public TelegramMultipartTransportContent Build(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        foreach (var property in TelegramRequestTypeMetadataCache.Get(payload.GetType()).TelegramProperties)
        {
            var value = property.GetValue(payload);
            if (value is null)
            {
                continue;
            }

            var fieldName = property.TelegramName;
            if (TelegramRequestUploadDetector.TryGetDirectInputFile(value, out var inputFile))
            {
                AddFile(fieldName, inputFile);
                continue;
            }

            var node = ToJsonNode(value);
            if (node is not null)
            {
                AddField(fieldName, node);
            }
        }

        return new TelegramMultipartTransportContent(_fields.ToArray(), _files.ToArray());
    }

    private JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        // Nested files are represented in JSON fields by attach:// names.
        // The binary stream is added to the same multipart body under that generated name.
        if (value is InputFile inputFile)
        {
            var fileName = "file" + _fileIndex.ToString(CultureInfo.InvariantCulture);
            _fileIndex++;
            AddFile(fileName, inputFile);
            return JsonValue.Create("attach://" + fileName);
        }

        if (value is string stringValue)
        {
            return JsonValue.Create(stringValue);
        }

        if (value is bool boolValue)
        {
            return JsonValue.Create(boolValue);
        }

        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            return JsonSerializer.SerializeToNode(value, value.GetType(), _serializerOptions);
        }

        if (value is IEnumerable enumerable and not string)
        {
            var array = new JsonArray();
            foreach (var item in enumerable)
            {
                array.Add(ToJsonNode(item));
            }

            return array;
        }

        var unionCase = TelegramRequestUploadDetector.GetActiveUnionCase(value);
        if (unionCase is not null)
        {
            // Telegram unions serialize as their selected value, not as the wrapper object.
            return ToJsonNode(unionCase.Value);
        }

        var metadata = TelegramRequestTypeMetadataCache.Get(value.GetType());
        if (metadata.TelegramProperties.Length == 0)
        {
            return JsonSerializer.SerializeToNode(value, value.GetType(), _serializerOptions);
        }

        var jsonObject = new JsonObject();
        foreach (var property in metadata.TelegramProperties)
        {
            var propertyValue = property.GetValue(value);
            if (propertyValue is null)
            {
                continue;
            }

            var node = ToJsonNode(propertyValue);
            if (node is not null)
            {
                jsonObject[property.TelegramName] = node;
            }
        }

        return jsonObject;
    }

    private void AddField(string name, JsonNode node)
    {
        var value = node switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringValue) => stringValue,
            JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolValue) => boolValue ? "true" : "false",
            _ => node.ToJsonString(_serializerOptions)
        };

        _fields.Add(new TelegramMultipartField(name, value));
    }

    private void AddFile(string name, InputFile inputFile)
    {
        if (!inputFile.Content.CanRead)
        {
            throw new InvalidOperationException(
                $"InputFile '{inputFile.FileName}' cannot be uploaded because its stream is not readable.");
        }

        if (!inputFile.Content.CanSeek)
        {
            throw new InvalidOperationException(
                $"InputFile '{inputFile.FileName}' cannot be uploaded by the default Telegram executor because its stream is not seekable. Use a seekable stream so retry behavior can resend the same content.");
        }

        inputFile.Content.Position = 0;
        _files.Add(new TelegramMultipartFile(name, inputFile.FileName, inputFile.Content));
    }
}
