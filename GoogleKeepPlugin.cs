using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;

namespace FirstPlugin;

public class GoogleKeepPlugin
{
    private readonly HttpClient _httpClient;

    public GoogleKeepPlugin()
    {
        _httpClient = new HttpClient();
    }

    [KernelFunction("list_notes")]
    [Description("Lists all notes from Google Keep.")]
    public async Task<List<GoogleKeepNote>> ListNotesAsync(/*[Description("The authentication token for Google Keep API")] string authToken*/)
    {
        // if (string.IsNullOrWhiteSpace(authToken))
        // {
        //     throw new ArgumentException("Authentication token must be provided.", nameof(authToken));
        // }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "ea4ae6fe72bd7bb028344573f5ab4e3e794cd0b7");

        var response = await _httpClient.GetAsync("https://keep.googleapis.com/v1/notes");

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to fetch notes: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync();
        var notes = JsonSerializer.Deserialize<GoogleKeepNotesResponse>(content);

        return notes?.Notes ?? new List<GoogleKeepNote>();
    }
}

public class GoogleKeepNotesResponse
{
    [JsonPropertyName("notes")]
    public List<GoogleKeepNote> Notes { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}

public class GoogleKeepNote
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("createTime")]
    public string CreateTime { get; set; } = string.Empty;

    [JsonPropertyName("updateTime")]
    public string UpdateTime { get; set; } = string.Empty;

    [JsonPropertyName("trashed")]
    public bool Trashed { get; set; }

    [JsonPropertyName("body")]
    public GoogleKeepNoteBody? Body { get; set; }
}

public class GoogleKeepNoteBody
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("list")]
    public GoogleKeepNoteListContent? List { get; set; }
}

public class GoogleKeepNoteListContent
{
    [JsonPropertyName("listItems")]
    public List<GoogleKeepNoteListItem> ListItems { get; set; } = new();
}

public class GoogleKeepNoteListItem
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("checked")]
    public bool Checked { get; set; }

    [JsonPropertyName("childListItems")]
    public List<GoogleKeepNoteListItem>? ChildListItems { get; set; }
}