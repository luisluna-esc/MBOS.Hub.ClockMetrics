using System.Net;
using System.Text.Json.Serialization;
using ClockMetrics.Core.Entities.ResponseApi.Details;

namespace ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;

public class ResponseGetObject
{
    public object Data { get; set; } = new();
    public Message[] Messages { get; set; } = [];
    [JsonIgnore]
    public HttpStatusCode StatusCode { get; set; }
}