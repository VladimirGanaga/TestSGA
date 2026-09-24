using System.Security.Cryptography;
using System.Text;
using AngleSharp.Html.Parser;
using Dapper;
using Npgsql;
using TestTask.Models;

namespace TestTask.Services;
public class ProcessingService : IProcessingService
{
    private readonly IConfiguration _configuration;
    private static readonly IHtmlParser HtmlParser = new HtmlParser();

    private static readonly System.Text.RegularExpressions.Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public ProcessingService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<ProcessResponseDto> ProcessAsync(ProcessRequestDto request)
    {
        try
        {
            var response = new ProcessResponseDto { IsError = 0 };

            string url;
            try
            {
                url = DecodeBase64(request.UrlB64!);
                response.Url = url;
            }
            catch (FormatException)
            {
                return CreateError("INVALID_URL_BASE64", "Failed to decode URL from Base64");
            }

            string pageHtml;
            try
            {
                pageHtml = DecodeBase64(request.PageB64!);
            }
            catch (FormatException)
            {
                return CreateError("INVALID_PAGE_BASE64", "Failed to decode Page from Base64");
            }

            var document = HtmlParser.ParseDocument(pageHtml);

            var elements = document.QuerySelectorAll(request.Selector!);
            response.ElementsCount = elements.Length;

            var attributeValues = new List<string>();
            foreach (var element in elements)
            {
                var value = element.GetAttribute(request.Attribute!);
                if (value != null)
                {
                    attributeValues.Add(value);
                }
            }
            response.ElementsAttrList = attributeValues;

            var emailMatches = EmailRegex.Matches(pageHtml);
            response.EmailsCount = emailMatches.Count;
            response.EmailsList = emailMatches.Select(m => m.Value).ToList();

            try
            {
                response.DecryptedPlainText = DecryptAesEcb(
                    request.EncryptedTextBytesB64!,
                    request.KeyBytesB64!);
            }
            catch (Exception ex)
            {
                return CreateError("AES_DECRYPT_FAILED", $"AES decryption failed: {ex.Message}");
            }

            try
            {
                await SaveElementsToDbAsync(attributeValues, elements);
            }
            catch (Exception ex)
            {
                return CreateError("DB_ERROR", $"Database error: {ex.Message}");
            }

            return response;
        }
        catch (Exception ex)
        {
            return CreateError("INTERNAL_ERROR", ex.Message);
        }
    }

    private static string DecodeBase64(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }

    private static string DecryptAesEcb(string encryptedTextB64, string keyB64)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedTextB64);
        var keyBytes = Convert.FromBase64String(keyB64);

        using var aes = Aes.Create();
        aes.Key = keyBytes;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private async Task SaveElementsToDbAsync(List<string> attributeValues, AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement> elements)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");

        var entities = new List<ElementEntity>();

        for (int i = 0; i < attributeValues.Count; i++)
        {
            entities.Add(new ElementEntity
            {
                AttributeValue = attributeValues[i],
                HtmlCode = elements[i].OuterHtml
            });
        }

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            @"INSERT INTO elements (attribute_value, html_code) 
              VALUES (@AttributeValue, @HtmlCode)",
            entities);
    }

    private static ProcessResponseDto CreateError(string code, string message)
    {
        return new ProcessResponseDto
        {
            IsError = 1,
            ErrorCode = code,
            ErrorMessage = message
        };
    }
}