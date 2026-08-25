using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OrderPlatform.Application.Orders;

/// <summary>ERP 接口服务：封装按客户名称取客户 ID、按图号+长度取货品、生成受订单表头与表身等调用。</summary>
public interface IErpPushService
{
    /// <summary>根据客户名称获取客户 ID。</summary>
    Task<ErpCustomerDto> GetCustomerAsync(string customerName, CancellationToken cancellationToken);

    /// <summary>根据图号 + 长度获取货品代号及基础信息。</summary>
    Task<ErpProductDto> GetProductAsync(string th, string pic, CancellationToken cancellationToken);

    /// <summary>生成受订单表头，返回受订单号。</summary>
    Task<ErpOrderHeaderDto> CreateOrderHeaderAsync(string cusNo, CancellationToken cancellationToken);

    /// <summary>生成受订单表身。</summary>
    Task<ErpItemResultDto> CreateOrderItemAsync(ErpOrderItemRequest request, CancellationToken cancellationToken);

    /// <summary>本次服务实例内发生的全部 ERP 调用轨迹（含原始响应报文）。</summary>
    List<ErpApiCall> Calls { get; }
}

/// <summary>单次 ERP 接口调用轨迹。</summary>
public class ErpApiCall
{
    /// <summary>接口名称（如 get_cus / MF_POS）。</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>实际发送的 JSON 请求报文（原文）。</summary>
    public string RequestJson { get; set; } = string.Empty;

    /// <summary>HTTP 状态码（未发出请求时为 null）。</summary>
    public int? HttpStatus { get; set; }

    /// <summary>ERP 原始响应报文（原文，不做任何加工）。</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>耗时（毫秒）。</summary>
    public long DurationMs { get; set; }

    /// <summary>是否成功。</summary>
    public bool Success { get; set; }

    /// <summary>异常信息（失败时）。</summary>
    public string? Error { get; set; }
}

/// <summary>ERP 客户信息。</summary>
public class ErpCustomerDto
{
    /// <summary>客户 ID。</summary>
    public string CusNo { get; set; } = string.Empty;

    /// <summary>客户名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户简称。</summary>
    public string Snm { get; set; } = string.Empty;
}

/// <summary>ERP 货品信息。</summary>
public class ErpProductDto
{
    /// <summary>货品代号。</summary>
    public string PrdNo { get; set; } = string.Empty;

    /// <summary>货品名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>图号。</summary>
    public string Snm { get; set; } = string.Empty;

    /// <summary>长度。</summary>
    public string PicNo { get; set; } = string.Empty;

    /// <summary>规格。</summary>
    public string Spc { get; set; } = string.Empty;
}

/// <summary>受订单表头结果。</summary>
public class ErpOrderHeaderDto
{
    /// <summary>受订单号。</summary>
    public string OsNo { get; set; } = string.Empty;
}

/// <summary>受订单表身请求。</summary>
public class ErpOrderItemRequest
{
    /// <summary>受订单号。</summary>
    public string SoNo { get; set; } = string.Empty;

    /// <summary>项次。</summary>
    public int Itm { get; set; }

    /// <summary>货品代号。</summary>
    public string PrdNo { get; set; } = string.Empty;

    /// <summary>支数（默认 1）。</summary>
    public decimal Qty { get; set; } = 1;

    /// <summary>重量（默认 0）。</summary>
    public decimal Qty1 { get; set; }

    /// <summary>预交日（yyyy-MM-dd）。</summary>
    public string Ydd { get; set; } = string.Empty;
}

/// <summary>受订单表身结果。</summary>
public class ErpItemResultDto
{
    /// <summary>结果信息。</summary>
    public string Jg { get; set; } = string.Empty;
}

/// <summary>ERP 接口服务实现：通过 HTTP POST JSON 报文调用 PHP 接口。</summary>
public class ErpPushService : IErpPushService
{
    // 请求 JSON 序列化：中文不转义（PHP json_decode 兼容两种写法，原文更可读）
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ErpPushService> _logger;

    public ErpPushService(HttpClient httpClient, ILogger<ErpPushService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>本次服务实例内发生的全部 ERP 调用轨迹（含原始响应报文）。</summary>
    public List<ErpApiCall> Calls { get; } = new();

    /// <summary>根据客户名称获取客户 ID。</summary>
    public async Task<ErpCustomerDto> GetCustomerAsync(string customerName, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object> { ["CUS_NAME"] = customerName };
        using var doc = await PostAsync("get_cus", payload, cancellationToken);
        return new ErpCustomerDto
        {
            CusNo = GetString(doc.RootElement, "CUS_NO"),
            Name = GetString(doc.RootElement, "NAME"),
            Snm = GetString(doc.RootElement, "SNM")
        };
    }

    /// <summary>根据图号 + 长度获取货品代号及基础信息。</summary>
    public async Task<ErpProductDto> GetProductAsync(string th, string pic, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>
        {
            ["TH"] = th,
            ["PIC"] = pic
        };
        using var doc = await PostAsync("get_PRD", payload, cancellationToken);
        return new ErpProductDto
        {
            PrdNo = GetString(doc.RootElement, "PRD_NO"),
            Name = GetString(doc.RootElement, "NAME"),
            Snm = GetString(doc.RootElement, "SNM"),
            PicNo = GetString(doc.RootElement, "PIC_NO"),
            Spc = GetString(doc.RootElement, "SPC")
        };
    }

    /// <summary>生成受订单表头。</summary>
    public async Task<ErpOrderHeaderDto> CreateOrderHeaderAsync(string cusNo, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object> { ["CUS_NO"] = cusNo };
        using var doc = await PostAsync("MF_POS", payload, cancellationToken);
        return new ErpOrderHeaderDto { OsNo = GetString(doc.RootElement, "OS_NO") };
    }

    /// <summary>生成受订单表身。</summary>
    public async Task<ErpItemResultDto> CreateOrderItemAsync(ErpOrderItemRequest request, CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object>
        {
            ["SO_NO"] = request.SoNo,
            ["ITM"] = request.Itm,
            ["PRD_NO"] = request.PrdNo,
            ["QTY"] = request.Qty,
            ["QTY1"] = request.Qty1,
            ["YDD"] = request.Ydd
        };
        using var doc = await PostAsync("TF_POS", payload, cancellationToken);
        var jg = GetString(doc.RootElement, "JG");
        if (string.IsNullOrWhiteSpace(jg) ||
            jg.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
            jg.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            jg.Contains("fail", StringComparison.OrdinalIgnoreCase))
        {
            Calls[^1].Success = false;
            Calls[^1].Error = $"ERP 接口 TF_POS 返回结果信息：{jg}";
            throw new InvalidOperationException($"ERP 接口 TF_POS 推送失败（项次 {request.Itm}）：{jg}");
        }

        return new ErpItemResultDto { Jg = jg };
    }

    /// <summary>发送 POST JSON 请求并解析响应，把请求与原始响应报文原文记入调用轨迹。</summary>
    private async Task<JsonDocument> PostAsync(string action, Dictionary<string, object> payload, CancellationToken cancellationToken)
    {
        var requestJson = JsonSerializer.Serialize(payload, RequestJsonOptions);
        _logger.LogInformation("ERP 请求 {Action} 报文: {Payload}", action, requestJson);

        var call = new ErpApiCall { Action = action, RequestJson = requestJson };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Calls.Add(call);

        string text;
        try
        {
            using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(action, content, cancellationToken);
            text = await response.Content.ReadAsStringAsync(cancellationToken);
            stopwatch.Stop();

            call.HttpStatus = (int)response.StatusCode;
            call.Response = text;
            call.DurationMs = stopwatch.ElapsedMilliseconds;

            if (!response.IsSuccessStatusCode)
            {
                call.Error = $"HTTP {(int)response.StatusCode}：{Truncate(text)}";
                throw new InvalidOperationException($"ERP 接口 {action} 返回 HTTP {(int)response.StatusCode}：{Truncate(text)}");
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            call.DurationMs = stopwatch.ElapsedMilliseconds;
            call.Success = false;
            call.Error = "ERP 接口 " + action + " 请求超时或被取消";
            throw;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            call.DurationMs = stopwatch.ElapsedMilliseconds;
            call.Success = false;
            call.Error = $"ERP 接口 {action} 网络错误：{ex.Message}";
            throw new InvalidOperationException($"ERP 接口 {action} 无法连接：{ex.Message}", ex);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            call.Success = false;
            call.Error = $"ERP 接口 {action} 返回非 JSON 内容";
            throw new InvalidOperationException($"ERP 接口 {action} 返回非 JSON 内容：{Truncate(text)}");
        }

        // 业务码校验：code 存在且不为 success 视为接口失败（带原始返回内容）
        var code = GetString(doc.RootElement, "code");
        if (!string.IsNullOrEmpty(code) && !string.Equals(code, "success", StringComparison.OrdinalIgnoreCase))
        {
            call.Success = false;
            call.Error = $"ERP 接口 {action} 返回 code={code}";
            throw new InvalidOperationException($"ERP 接口 {action} 返回失败（code={code}）：{Truncate(text)}");
        }

        call.Success = true;
        return doc;
    }

    /// <summary>忽略大小写读取响应字段（兼容外层 data 包装；data 为数组时取第一个元素）。</summary>
    private static string GetString(JsonElement root, string key)
    {
        var target = root;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            target = data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
                ? data[0]
                : data;
        }

        if (target.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        foreach (var prop in target.EnumerateObject())
        {
            if (!string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => prop.Value.GetRawText()
            };
        }

        return string.Empty;
    }

    /// <summary>截断超长响应文本，避免日志/错误信息过大。</summary>
    private static string Truncate(string text)
    {
        return text.Length <= 500 ? text : text[..500] + "...";
    }
}