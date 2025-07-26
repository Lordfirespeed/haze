using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Haze.Util;

public class StandardJsonResult : JsonResult
{
    private const int DefaultStatusCode = 200;

    private readonly ObjectWrapper _wrapper;

    public StandardJsonResult(object? value, object? serializerSettings = null) : base(null, serializerSettings)
    {
        _wrapper = new() {
            Data = value,
        };
        StatusCode = DefaultStatusCode;
        Value = _wrapper;
    }

    public new int StatusCode {
        get => _wrapper.StatusCode ?? throw new InvalidOperationException();
        set {
            base.StatusCode = value;
            _wrapper.StatusCode = value;
            _wrapper.Message = ReasonPhrases.GetReasonPhrase(value);
        }
    }

    public string? Detail {
        get => _wrapper.Detail;
        set => _wrapper.Detail = value;
    }

    class ObjectWrapper
    {
        [JsonPropertyName("status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? StatusCode { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        [JsonPropertyName("detail")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Detail { get; set; }

        [JsonPropertyName("data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? Data { get; set; }
    }
}
