using System.Text.Json.Serialization;
using FastEndpoints.Security;

namespace HeadlessCms.Api.Auth.Models;

public class SessionTokens : TokenResponse
{
    [JsonPropertyName("accessExpiry")]
    public DateTime SerializedAccessExpiry => base.AccessExpiry;

    [JsonPropertyName("refreshExpiry")]
    public DateTime SerializedRefreshExpiry => base.RefreshExpiry;
}