using OrderPlatform.Domain.Enums;

namespace OrderPlatform.Domain.Entities;

/// <summary>上传批次实体，一次上传一个文件对应一个批次，记录解析进度与结果。</summary>
public class UploadBatch
{
    /// <summary>批次唯一标识。</summary>
    public Guid Id { get; set; }

    /// <summary>批次号（生成序号，唯一）。</summary>
    public string BatchNo { get; set; } = string.Empty;

    /// <summary>文件类型：PDF 订单 / Excel 客户资料。</summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>原始文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>识别出的客户（PDF 按采购方名、Excel 按 sheet 名）。</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>上传人。</summary>
    public Guid UploadUserId { get; set; }

    /// <summary>解析状态：Pending / Parsing / Completed / Failed。</summary>
    public UploadStatus Status { get; set; }

    /// <summary>解析进度（0-100）。</summary>
    public int Progress { get; set; }

    /// <summary>失败原因或重复导入提示。</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>解析结果原始 JSON（PDF 明细 / Excel 明细），用于补匹配与详情查看。</summary>
    public string? RawDataJson { get; set; }

    /// <summary>保存文件的相对路径。</summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>创建时间。</summary>
    public DateTime CreatedAt { get; set; }
}