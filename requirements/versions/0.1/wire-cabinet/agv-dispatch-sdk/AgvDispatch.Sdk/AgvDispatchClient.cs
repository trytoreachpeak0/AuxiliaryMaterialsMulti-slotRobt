using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgvDispatch.Sdk.Exceptions;
using AgvDispatch.Sdk.Json;
using AgvDispatch.Sdk.Models;
using Microsoft.Extensions.Options;

namespace AgvDispatch.Sdk;

public sealed class AgvDispatchClient : IAgvDispatchClient, IDisposable
{
    private readonly AgvDispatchOptions _options;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private string? _accessToken;

    /// <summary>供 DI / IHttpClientFactory 使用。</summary>
    public AgvDispatchClient(HttpClient httpClient, IOptions<AgvDispatchOptions> options)
        : this(options?.Value ?? throw new ArgumentNullException(nameof(options)), httpClient, ownsHttpClient: false)
    {
    }

    /// <summary>无 DI 时手动创建（自行 Dispose）。</summary>
    public static AgvDispatchClient Create(AgvDispatchOptions options)
    {
        options.Validate();
        var http = new HttpClient { BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/") };
        http.Timeout = options.HttpTimeout;
        return new AgvDispatchClient(options, http, ownsHttpClient: true);
    }

    private AgvDispatchClient(AgvDispatchOptions options, HttpClient httpClient, bool ownsHttpClient)
    {
        _options = options;
        _http = httpClient;
        _ownsHttpClient = ownsHttpClient;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = _options.HttpTimeout;
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var body = new LoginRequest { Username = _options.Username, Password = _options.Password };
        using var response = await _http.PostAsJsonAsync(
            "api/auth/v1/admin/login",
            body,
            AgvDispatchJson.SerializerOptions,
            cancellationToken).ConfigureAwait(false);

        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);

        _accessToken = ExtractToken(raw);
        if (string.IsNullOrEmpty(_accessToken))
            throw new AgvDispatchException("Login succeeded but token not found in response.", (int)response.StatusCode, null, null, raw);
    }

    public async Task<VehicleInfoDto> GetVehicleInfoAsync(string? deviceKey = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var key = deviceKey ?? _options.DefaultDeviceKey;
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"api/task/vehicles/getVehicleInfoByDeviceKey?key={Uri.EscapeDataString(key)}");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
        return DeserializeVehicleInfo(raw);
    }

    public async Task<IReadOnlyList<VehicleListItemDto>> GetVehiclesAsync(int[]? deviceIds = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var ids = deviceIds ?? _options.VehicleQueryDeviceIds;
        var query = string.Join(",", ids);
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"api/task/vehicles?deviceIds={query}&groupIds=&pageNum=-1");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);

        var wrapped = JsonSerializer.Deserialize<DispatchApiResponse<List<VehicleListItemDto>>>(raw, AgvDispatchJson.SerializerOptions);
        if (wrapped?.Result != null)
            return wrapped.Result;

        throw new AgvDispatchException("Vehicle list result is empty.", (int)response.StatusCode, wrapped?.Code, wrapped?.Message, raw);
    }

    public async Task<OrderDetailDto> GetOrderDetailAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"api/order/v1/orderRecord/detailByOrderId/{Uri.EscapeDataString(orderId)}");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
        return DeserializeOrderDetail(raw);
    }

    public async Task<VehicleSnapshot> GetVehicleSnapshotAsync(string? deviceKey = null, CancellationToken cancellationToken = default)
    {
        var vehicle = await GetVehicleInfoAsync(deviceKey, cancellationToken).ConfigureAwait(false);
        OrderDetailDto? order = null;
        if (!string.IsNullOrWhiteSpace(vehicle.OrderTaskId))
            order = await GetOrderDetailAsync(vehicle.OrderTaskId, cancellationToken).ConfigureAwait(false);
        return new VehicleSnapshot { Vehicle = vehicle, Order = order };
    }

    public async Task<CreateOrderResult> CreateMoveOrderAsync(
        int destination,
        int? mapId = null,
        string? deviceKey = null,
        string? orderName = null,
        CancellationToken cancellationToken = default)
    {
        if (destination == 0)
            throw new ArgumentException("destination must not be 0.", nameof(destination));

        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var body = new CreateMoveOrderRequest
        {
            OrderName = orderName ?? _options.DefaultOrderName,
            AppointVehicleKey = deviceKey ?? _options.DefaultDeviceKey,
            Mission =
            {
                new MissionItemDto
                {
                    Type = "move",
                    Destination = destination,
                    MapId = mapId ?? _options.DefaultMapId
                }
            }
        };

        return await PostCreateOrderAsync(body, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CreateOrderResult> CreateChargeOrderAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var body = new CreateMoveOrderRequest
        {
            OrderName = _options.DefaultOrderName,
            AppointVehicleKey = _options.DefaultDeviceKey,
            Mission =
            {
                new MissionItemDto
                {
                    Type = "move",
                    Destination = _options.ChargeDestination,
                    MapId = _options.DefaultMapId
                },
                new MissionItemDto
                {
                    Type = "act",
                    ActionId = _options.ChargeActionId,
                    ActionName = _options.ChargeActionName,
                    ActionParam1 = _options.ChargeActionParam1,
                    ActionParam2 = _options.ChargeActionParam2
                }
            }
        };

        return await PostCreateOrderAsync(body, cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var body = new CancelOrderCommandRequest();
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"api/task/v1/order/command/{Uri.EscapeDataString(orderId)}");
        request.Content = JsonContent.Create(body, options: AgvDispatchJson.SerializerOptions);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
    }

    public async Task PauseMovementAsync(string? deviceKey = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var key = deviceKey ?? _options.DefaultDeviceKey;
        var body = new ServiceCommandBody { MessageId = _options.PauseMovementMessageId, ThingsProperties = new { } };
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"api/device/v1/command/sync/service/{Uri.EscapeDataString(key)}/pauseMovement");
        request.Content = JsonContent.Create(body, options: AgvDispatchJson.SerializerOptions);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
    }

    public async Task ContinueMovementAsync(string? deviceKey = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var key = deviceKey ?? _options.DefaultDeviceKey;
        var body = new ServiceCommandBody { MessageId = _options.ContinueMovementMessageId, ThingsProperties = new { } };
        using var request = CreateAuthorizedRequest(HttpMethod.Post, $"api/device/v1/command/sync/service/{Uri.EscapeDataString(key)}/continueMovement");
        request.Content = JsonContent.Create(body, options: AgvDispatchJson.SerializerOptions);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
    }

    public async Task CancelEmergencyAsync(string? deviceKey = null, CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticatedAsync(cancellationToken).ConfigureAwait(false);
        var key = deviceKey ?? _options.DefaultDeviceKey;
        var body = new CancelEmergencyRequest
        {
            DeviceCommandDto = new DeviceCommandDto { MessageId = _options.CancelEmergencyMessageId },
            DeviceKeys = { key },
            ServiceId = "cancelEmergency"
        };
        using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/device/v1/command/batchServiceSet");
        request.Content = JsonContent.Create(body, options: AgvDispatchJson.SerializerOptions);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }

    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_accessToken))
            await LoginAsync(cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<CreateOrderResult> PostCreateOrderAsync(CreateMoveOrderRequest body, CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Post, "api/order/v1/add/byDefaultMissions");
        request.Content = JsonContent.Create(body, options: AgvDispatchJson.SerializerOptions);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, raw, cancellationToken).ConfigureAwait(false);
        return new CreateOrderResult { RawResponse = raw, OrderId = TryExtractOrderId(raw) };
    }

    private static Task EnsureSuccessAsync(HttpResponseMessage response, string raw, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return Task.CompletedTask;

        DispatchApiResponse<object>? err = null;
        try
        {
            err = JsonSerializer.Deserialize<DispatchApiResponse<object>>(raw, AgvDispatchJson.SerializerOptions);
        }
        catch
        {
            // ignore
        }

        throw new AgvDispatchException(
            $"Dispatch API failed: {(int)response.StatusCode} {response.ReasonPhrase}",
            (int)response.StatusCode,
            err?.Code,
            err?.Message ?? raw,
            raw);
    }

    private static VehicleInfoDto DeserializeVehicleInfo(string raw)
    {
        var wrapped = JsonSerializer.Deserialize<DispatchApiResponse<VehicleInfoDto>>(raw, AgvDispatchJson.SerializerOptions);
        if (wrapped?.Result != null)
            return wrapped.Result;

        var direct = JsonSerializer.Deserialize<VehicleInfoDto>(raw, AgvDispatchJson.SerializerOptions);
        if (direct != null && (direct.DeviceKey != null || direct.SysState != null))
            return direct;

        throw new AgvDispatchException("Unable to deserialize vehicle info.", null, null, null, raw);
    }

    private static OrderDetailDto DeserializeOrderDetail(string raw)
    {
        var wrapped = JsonSerializer.Deserialize<DispatchApiResponse<OrderDetailDto>>(raw, AgvDispatchJson.SerializerOptions);
        if (wrapped?.Result != null)
            return wrapped.Result;

        var direct = JsonSerializer.Deserialize<OrderDetailDto>(raw, AgvDispatchJson.SerializerOptions);
        if (direct != null)
            return direct;

        throw new AgvDispatchException("Unable to deserialize order detail.", null, null, null, raw);
    }

    private static string? ExtractToken(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("token", out var t))
                return t.GetString();
            if (doc.RootElement.TryGetProperty("result", out var r) && r.TryGetProperty("token", out var rt))
                return rt.GetString();
        }
        catch
        {
            // fall through
        }

        return null;
    }

    private static string? TryExtractOrderId(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            foreach (var name in new[] { "orderId", "orderTaskId", "id" })
            {
                if (doc.RootElement.TryGetProperty("result", out var res) && res.TryGetProperty(name, out var v))
                    return v.ToString();
                if (doc.RootElement.TryGetProperty(name, out var root))
                    return root.ToString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
