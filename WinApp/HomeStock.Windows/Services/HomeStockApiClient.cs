using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeStock.Windows.Models;

namespace HomeStock.Windows.Services;

/// <summary>Home Stock APIの認証更新と在庫操作を提供します。</summary>
public sealed class HomeStockApiClient(HttpClient httpClient, ICredentialStore credentialStore)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private LoginResponse? _tokens;
    private StoredSession? _session;
    private Uri? _apiBaseUri;

    public async Task LoginAsync(Uri apiBaseUri, string username, string password, CancellationToken cancellationToken)
    {
        _apiBaseUri = NormalizeBaseUri(apiBaseUri);
        var response = await httpClient.PostAsJsonAsync(
            CreateRequestUri("api/auth/login"),
            new LoginRequest(username, password),
            JsonOptions,
            cancellationToken);
        _tokens = await ReadRequiredAsync<LoginResponse>(response, cancellationToken);
        _session = new StoredSession(_apiBaseUri, username, _tokens.RefreshToken);
        credentialStore.Save(_session);
    }

    public async Task<bool> RestoreAsync(CancellationToken cancellationToken)
    {
        _session = credentialStore.Read();
        if (_session is null)
        {
            return false;
        }

        _apiBaseUri = NormalizeBaseUri(_session.ApiBaseUri);
        try
        {
            await RefreshAsync(cancellationToken);
            return true;
        }
        catch (ApiException)
        {
            credentialStore.Delete();
            _session = null;
            return false;
        }
    }

    public void Logout()
    {
        _tokens = null;
        _session = null;
        credentialStore.Delete();
    }

    public async Task<string> RegisterInitialUserAsync(Uri apiBaseUri, string username, string password, string? setupToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(NormalizeBaseUri(apiBaseUri), "api/users/register"))
        {
            Content = JsonContent.Create(new RegisterUserRequest(username, password), options: JsonOptions),
        };
        if (!string.IsNullOrWhiteSpace(setupToken))
        {
            request.Headers.Add("X-Setup-Token", setupToken.Trim());
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        return (await ReadRequiredAsync<MessageResponse>(response, cancellationToken)).Message;
    }

    public Task<DashboardResponse> GetDashboardAsync(CancellationToken cancellationToken) =>
        GetAsync<DashboardResponse>("api/dashboard", cancellationToken);

    public Task<IReadOnlyList<ProductResponse>> GetProductsAsync(string? query, string? barcode, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<ProductResponse>>($"api/products?query={Uri.EscapeDataString(query ?? string.Empty)}&barcode={Uri.EscapeDataString(barcode ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<LocationResponse>> GetLocationsAsync(CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<LocationResponse>>("api/locations", cancellationToken);

    public Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken) =>
        PostAsync<CreateProductRequest, ProductResponse>("api/products", request, cancellationToken);

    public Task<ProductResponse> UpdateProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken) =>
        SendWithResponseAsync<UpdateProductRequest, ProductResponse>(HttpMethod.Patch, $"api/products/{productId}", request, cancellationToken);

    public Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Delete, $"api/products/{productId}", cancellationToken);

    public Task<IReadOnlyList<DeletedProductSuggestionResponse>> GetDeletedProductSuggestionsAsync(string name, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<DeletedProductSuggestionResponse>>($"api/products/deleted-suggestions?name={Uri.EscapeDataString(name)}", cancellationToken);

    public Task<ProductResponse> RestoreProductAsync(Guid productId, UpdateProductRequest request, CancellationToken cancellationToken) =>
        PostAsync<UpdateProductRequest, ProductResponse>($"api/products/{productId}/restore", request, cancellationToken);

    public Task<LocationResponse> CreateLocationAsync(CreateLocationRequest request, CancellationToken cancellationToken) =>
        PostAsync<CreateLocationRequest, LocationResponse>("api/locations", request, cancellationToken);

    public Task<LocationResponse> UpdateLocationAsync(Guid locationId, UpdateLocationRequest request, CancellationToken cancellationToken) =>
        SendWithResponseAsync<UpdateLocationRequest, LocationResponse>(HttpMethod.Patch, $"api/locations/{locationId}", request, cancellationToken);

    public Task DeleteLocationAsync(Guid locationId, CancellationToken cancellationToken) =>
        SendWithoutBodyAsync(HttpMethod.Delete, $"api/locations/{locationId}", cancellationToken);

    public Task<IReadOnlyList<DeletedLocationSuggestionResponse>> GetDeletedLocationSuggestionsAsync(string name, CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<DeletedLocationSuggestionResponse>>($"api/locations/deleted-suggestions?name={Uri.EscapeDataString(name)}", cancellationToken);

    public Task<LocationResponse> RestoreLocationAsync(Guid locationId, UpdateLocationRequest request, CancellationToken cancellationToken) =>
        PostAsync<UpdateLocationRequest, LocationResponse>($"api/locations/{locationId}/restore", request, cancellationToken);

    public Task<IReadOnlyList<InventoryLotResponse>> GetInventoryAsync(
        string? query,
        string sortBy,
        string sortOrder,
        int limit,
        CancellationToken cancellationToken) =>
        GetAsync<IReadOnlyList<InventoryLotResponse>>(
            $"api/inventory?query={Uri.EscapeDataString(query ?? string.Empty)}&sortBy={Uri.EscapeDataString(sortBy)}&sortOrder={Uri.EscapeDataString(sortOrder)}&limit={limit}",
            cancellationToken);

    public Task<ShoppingListResponse> GetShoppingListAsync(CancellationToken cancellationToken) =>
        GetAsync<ShoppingListResponse>("api/shopping-lists/current", cancellationToken);

    public Task<ShoppingListResponse> AddShoppingItemAsync(AddShoppingItemRequest request, CancellationToken cancellationToken) =>
        PostAsync<AddShoppingItemRequest, ShoppingListResponse>("api/shopping-lists/current/items", request, cancellationToken);

    public Task<ShoppingListResponse> GenerateShoppingSuggestionsAsync(CancellationToken cancellationToken) =>
        PostAsync<object, ShoppingListResponse>("api/shopping-lists/current/generate", new { }, cancellationToken);

    public Task<ShoppingListResponse> UpdateShoppingItemAsync(Guid itemId, UpdateShoppingItemRequest request, CancellationToken cancellationToken) =>
        SendWithResponseAsync<UpdateShoppingItemRequest, ShoppingListResponse>(HttpMethod.Patch, $"api/shopping-lists/current/items/{itemId}", request, cancellationToken);

    public Task<PrintPreviewResponse> GetPrintPreviewAsync(PrintPaperWidth paperWidth, CancellationToken cancellationToken) =>
        GetAsync<PrintPreviewResponse>($"api/shopping-lists/current/print-preview?paperWidth={paperWidth}", cancellationToken);

    public Task<PrintJobResponse> CreatePrintJobAsync(PrintPaperWidth paperWidth, CancellationToken cancellationToken) =>
        PostAsync<CreatePrintJobRequest, PrintJobResponse>(
            "api/shopping-lists/current/print-jobs",
            new CreatePrintJobRequest(paperWidth),
            cancellationToken);

    public Task ReceiveAsync(ReceiveStockRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/inventory/receive", request, idempotencyKey, cancellationToken);

    public Task ConsumeAsync(ConsumeStockRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/inventory/consume", request, idempotencyKey, cancellationToken);

    public Task AdjustAsync(AdjustStockRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        SendAsync(HttpMethod.Post, "api/inventory/adjust", request, idempotencyKey, cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Get, CreateRequestUri(path)), cancellationToken);
        return await ReadRequiredAsync<T>(response, cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
        => await SendWithResponseAsync<TRequest, TResponse>(HttpMethod.Post, path, body, cancellationToken);

    private async Task<TResponse> SendWithResponseAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest body, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(method, CreateRequestUri(path)) { Content = JsonContent.Create(body, options: JsonOptions) },
            cancellationToken);
        return await ReadRequiredAsync<TResponse>(response, cancellationToken);
    }

    private async Task SendAsync<T>(HttpMethod method, string path, T body, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(() =>
        {
            var request = new HttpRequestMessage(method, CreateRequestUri(path)) { Content = JsonContent.Create(body) };
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return request;
        }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task SendWithoutBodyAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        using var response = await SendAuthorizedAsync(
            () => new HttpRequestMessage(method, CreateRequestUri(path)),
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> createRequest, CancellationToken cancellationToken)
    {
        if (_tokens is null)
        {
            throw new ApiException("ログインが必要です。");
        }

        var response = await SendOnceAsync(createRequest(), cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        await RefreshAsync(cancellationToken);
        return await SendOnceAsync(createRequest(), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens!.Token);
        return await httpClient.SendAsync(request, cancellationToken);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var refreshToken = _tokens?.RefreshToken ?? _session?.RefreshToken ?? throw new ApiException("保存された認証情報がありません。");
        using var response = await httpClient.PostAsJsonAsync(
            CreateRequestUri("api/auth/refresh"),
            new RefreshTokenRequest(refreshToken),
            JsonOptions,
            cancellationToken);
        _tokens = await ReadRequiredAsync<LoginResponse>(response, cancellationToken);
        _session = _session! with { RefreshToken = _tokens.RefreshToken };
        credentialStore.Save(_session);
    }

    private static Uri NormalizeBaseUri(Uri uri) => new(uri.AbsoluteUri.TrimEnd('/') + "/");

    private Uri CreateRequestUri(string path) => new(
        _apiBaseUri ?? throw new ApiException("APIの接続先が設定されていません。"),
        path);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken) ?? throw new ApiException("API応答が空です。");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ApiException($"APIエラー ({(int)response.StatusCode}): {body}");
    }
}

public sealed class ApiException(string message) : Exception(message);
