using System.Text.Json.Serialization;

namespace TestTask.Models;

public class ProcessResponseDto
{
    [JsonPropertyName("is_error")]
    public int IsError { get; set; }

    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("elements_count")]
    public int ElementsCount { get; set; }

    [JsonPropertyName("emails_count")]
    public int EmailsCount { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("decrypted_plain_text")]
    public string? DecryptedPlainText { get; set; }

    [JsonPropertyName("elements_attr_list")]
    public List<string> ElementsAttrList { get; set; } = new();

    [JsonPropertyName("emails_list")]
    public List<string> EmailsList { get; set; } = new();
}