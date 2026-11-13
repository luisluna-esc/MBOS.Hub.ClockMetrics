using System.Net;
using System.Text.Json.Serialization;
using ClockMetrics.Core.Entities.ResponseApi.Details;

namespace ClockMetrics.Core.Entities.ResponseApi.DisplayFormat;

public class ResponsePost
{
    public Message[] Messages { get; set; } = [];
    public long Id { get; set; }
    [JsonIgnore]
    public HttpStatusCode StatusCode { get; set; }
}