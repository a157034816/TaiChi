using CentralService.Admin;
using CentralService.Admin.Config;
using CentralService.Admin.Models;
using CentralService.Service.Models;
using ApiResponseFactory = CentralService.Internal.CentralServiceApiResponseFactory;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace CentralService.Controllers.Admin;

[ApiController]
[Route("api/admin/config")]
[Produces("application/json")]
[EnableCors("CentralServiceAdmin")]
[Authorize(
    AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme,
    Policy = CentralServicePermissions.Config.Read)]
public sealed class ConfigController : ControllerBase
{
    private readonly CentralServiceConfigService _configService;

    public ConfigController(CentralServiceConfigService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<CurrentConfigResponse>>> GetCurrent()
    {
        var current = await _configService.GetCurrentVersionAsync();
        if (current == null)
        {
            return Ok(ApiResponseFactory.Success(
                new CurrentConfigResponse(null, null, CentralServiceRuntimeConfigJson.DefaultJson)));
        }

        return Ok(ApiResponseFactory.Success(
            new CurrentConfigResponse(current.Id, current.VersionNo, current.ConfigJson)));
    }

    [HttpGet("versions")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ConfigVersionListItem>>>> ListVersions()
    {
        var versions = await _configService.ListVersionsAsync();
        var items = versions
            .Select(x => new ConfigVersionListItem(
                x.Id,
                x.VersionNo,
                x.Status,
                x.Comment,
                x.BasedOnVersionId,
                x.CreatedAtUtc,
                x.CreatedByUsername,
                x.UpdatedAtUtc,
                x.UpdatedByUsername,
                x.PublishedAtUtc,
                x.PublishedByUsername))
            .ToArray();

        return Ok(ApiResponseFactory.Success<IReadOnlyList<ConfigVersionListItem>>(items));
    }

    [HttpGet("versions/{id:int}")]
    public async Task<ActionResult<ApiResponse<ConfigVersionDetail>>> GetVersion(int id)
    {
        var version = await _configService.GetVersionAsync(id);
        if (version == null)
        {
            return NotFound(ApiResponseFactory.Error<ConfigVersionDetail>("配置版本不存在", 404));
        }

        return Ok(ApiResponseFactory.Success(new ConfigVersionDetail(
            version.Id,
            version.VersionNo,
            version.Status,
            version.Comment,
            version.BasedOnVersionId,
            version.CreatedAtUtc,
            version.CreatedByUsername,
            version.UpdatedAtUtc,
            version.UpdatedByUsername,
            version.PublishedAtUtc,
            version.PublishedByUsername,
            version.ConfigJson)));
    }

    [HttpPost("versions")]
    [Authorize(Policy = CentralServicePermissions.Config.Edit)]
    public async Task<ActionResult<ApiResponse<ConfigVersionDetail>>> CreateDraft([FromBody] CreateConfigDraftRequest request)
    {
        var draft = await _configService.CreateDraftAsync(request?.Comment, request?.BasedOnVersionId);

        return Ok(ApiResponseFactory.Success(new ConfigVersionDetail(
            draft.Id,
            draft.VersionNo,
            draft.Status,
            draft.Comment,
            draft.BasedOnVersionId,
            draft.CreatedAtUtc,
            draft.CreatedByUsername,
            draft.UpdatedAtUtc,
            draft.UpdatedByUsername,
            draft.PublishedAtUtc,
            draft.PublishedByUsername,
            draft.ConfigJson)));
    }

    [HttpPut("versions/{id:int}")]
    [Authorize(Policy = CentralServicePermissions.Config.Edit)]
    public async Task<ActionResult<ApiResponse<ConfigVersionDetail>>> UpdateDraft(int id, [FromBody] UpdateConfigDraftRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ConfigJson))
        {
            return BadRequest(ApiResponseFactory.Error<ConfigVersionDetail>("ConfigJson 不能为空", 400));
        }

        try
        {
            var updated = await _configService.UpdateDraftAsync(id, request.ConfigJson, request.Comment);

            return Ok(ApiResponseFactory.Success(new ConfigVersionDetail(
                updated.Id,
                updated.VersionNo,
                updated.Status,
                updated.Comment,
                updated.BasedOnVersionId,
                updated.CreatedAtUtc,
                updated.CreatedByUsername,
                updated.UpdatedAtUtc,
                updated.UpdatedByUsername,
                updated.PublishedAtUtc,
                updated.PublishedByUsername,
                updated.ConfigJson)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseFactory.Error<ConfigVersionDetail>(ex.Message, 400));
        }
    }

    [HttpPost("versions/{id:int}/validate")]
    [Authorize(Policy = CentralServicePermissions.Config.Edit)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<string>>>> ValidateVersion(int id)
    {
        var version = await _configService.GetVersionAsync(id);
        if (version == null)
        {
            return NotFound(ApiResponseFactory.Error<IReadOnlyList<string>>("配置版本不存在", 404));
        }

        var errors = CentralServiceRuntimeConfigValidator.ValidateJson(version.ConfigJson);
        return Ok(ApiResponseFactory.Success<IReadOnlyList<string>>(errors));
    }

    [HttpGet("versions/{id:int}/diff")]
    public async Task<ActionResult<ApiResponse<ConfigDiffResponse>>> Diff(int id, [FromQuery] int? baseVersionId = null)
    {
        var target = await _configService.GetVersionAsync(id);
        if (target == null)
        {
            return NotFound(ApiResponseFactory.Error<ConfigDiffResponse>("配置版本不存在", 404));
        }

        CentralService.Admin.Entities.CentralServiceConfigVersion? baseVersion;
        if (baseVersionId != null)
        {
            baseVersion = await _configService.GetVersionAsync(baseVersionId.Value);
        }
        else
        {
            baseVersion = await _configService.GetCurrentVersionAsync();
        }

        if (baseVersion == null)
        {
            return Ok(ApiResponseFactory.Success(new ConfigDiffResponse(
                BaseVersionId: 0,
                TargetVersionId: target.Id,
                BaseJson: CentralServiceRuntimeConfigJson.DefaultJson,
                TargetJson: target.ConfigJson)));
        }

        return Ok(ApiResponseFactory.Success(new ConfigDiffResponse(
            baseVersion.Id,
            target.Id,
            baseVersion.ConfigJson,
            target.ConfigJson)));
    }

    [HttpPost("versions/{id:int}/publish")]
    [Authorize(Policy = CentralServicePermissions.Config.Publish)]
    public async Task<ActionResult<ApiResponse<object>>> Publish(int id, [FromBody] PublishConfigRequest request)
    {
        try
        {
            await _configService.PublishAsync(id, request?.Note);
            return Ok(ApiResponseFactory.Success<object>(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseFactory.Error<object>(ex.Message, 400));
        }
    }

    [HttpPost("versions/{id:int}/rollback")]
    [Authorize(Policy = CentralServicePermissions.Config.Rollback)]
    public async Task<ActionResult<ApiResponse<object>>> Rollback(int id, [FromBody] RollbackConfigRequest request)
    {
        try
        {
            await _configService.RollbackAsync(id, request?.Note);
            return Ok(ApiResponseFactory.Success<object>(new { }));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponseFactory.Error<object>(ex.Message, 400));
        }
    }

    [HttpGet("publish-history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PublishHistoryItem>>>> PublishHistory([FromQuery] int take = 200)
    {
        var list = await _configService.ListPublishHistoryAsync(take);
        var items = list
            .Select(x => new PublishHistoryItem(
                x.Id,
                x.Action,
                x.FromVersionId,
                x.ToVersionId,
                x.Note,
                x.ActorUsername,
                x.CreatedAtUtc))
            .ToArray();

        return Ok(ApiResponseFactory.Success<IReadOnlyList<PublishHistoryItem>>(items));
    }
}

