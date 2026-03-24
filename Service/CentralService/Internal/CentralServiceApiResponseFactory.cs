using CentralService.Service.Models;

namespace CentralService.Internal;

/// <summary>
/// 为服务端控制器创建统一的 API 响应对象。
/// </summary>
internal static class CentralServiceApiResponseFactory
{
    /// <summary>
    /// 创建成功响应。
    /// </summary>
    public static ApiResponse<T> Success<T>(T data)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data
        };
    }

    /// <summary>
    /// 创建失败响应。
    /// </summary>
    public static ApiResponse<T> Error<T>(string errorMessage, int errorCode = 500, string? errorKey = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorKey = errorKey ?? string.Empty,
            ErrorMessage = errorMessage
        };
    }
}
