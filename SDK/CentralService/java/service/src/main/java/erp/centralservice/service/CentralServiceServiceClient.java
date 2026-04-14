package erp.centralservice.service;

import erp.centralservice.service.errors.CentralServiceException;
import erp.centralservice.service.internal.CentralServiceErrorParser;
import erp.centralservice.service.internal.CentralServiceHttpClient;
import erp.centralservice.service.internal.CentralServiceJson;
import erp.centralservice.service.models.ApiResponse;
import erp.centralservice.service.models.ServiceRegistrationRequest;
import erp.centralservice.service.models.ServiceRegistrationResponse;

import java.util.Map;

/**
 * 面向服务注册侧的 Central Service SDK 客户端。
 */
public final class CentralServiceServiceClient {
    private final CentralServiceHttpClient http;

    public CentralServiceServiceClient(String baseUrl) {
        this(new CentralServiceSdkOptions(baseUrl));
    }

    public CentralServiceServiceClient(CentralServiceSdkOptions options) {
        if (options == null) throw new IllegalArgumentException("options is required");
        this.http = new CentralServiceHttpClient(options);
    }

    public ServiceRegistrationResponse register(ServiceRegistrationRequest request) {
        if (request == null) throw new IllegalArgumentException("request is required");

        CentralServiceHttpClient.Response response = send(
            "POST",
            "/api/Service/register",
            CentralServiceJson.stringify(request.toJson()));

        ApiResponse<ServiceRegistrationResponse> api = parseApiResponse(response.body, new ApiResponseDataParser<ServiceRegistrationResponse>() {
            @Override
            public ServiceRegistrationResponse parse(Object data) {
                return ServiceRegistrationResponse.fromJson(data);
            }
        });
        if (api == null) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.unknown("POST", response.url, response.statusCode, response.body, "无法解析响应"),
                    response));
        }
        if (!api.success) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.parse("POST", response.url, response.statusCode, response.body),
                    response));
        }
        return api.data;
    }

    public void deregister(String serviceId) {
        if (serviceId == null || serviceId.trim().isEmpty()) throw new IllegalArgumentException("serviceId is required");

        CentralServiceHttpClient.Response response = send(
            "DELETE",
            "/api/Service/deregister/" + CentralServiceHttpClient.encodePathSegment(serviceId),
            null);

        ApiResponse<Object> api = parseApiResponse(response.body, new ApiResponseDataParser<Object>() {
            @Override
            public Object parse(Object data) {
                return data;
            }
        });
        if (api != null && !api.success) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.parse("DELETE", response.url, response.statusCode, response.body),
                    response));
        }
    }

    private CentralServiceHttpClient.Response send(String method, String path, String body) {
        final CentralServiceHttpClient.Response response;
        try {
            response = http.send(method, path, body);
        } catch (RuntimeException ex) {
            throw new CentralServiceException(CentralServiceHttpClient.createTransportError(method, path, ex));
        }

        if (!isSuccess(response.statusCode)) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.parse(method, response.url, response.statusCode, response.body),
                    response));
        }
        return response;
    }

    private interface ApiResponseDataParser<T> {
        T parse(Object data);
    }

    private static <T> ApiResponse<T> parseApiResponse(String json, ApiResponseDataParser<T> dataParser) {
        if (json == null || json.trim().isEmpty()) return null;
        Object root = CentralServiceJson.parse(json);
        Map<String, Object> obj = CentralServiceJson.asObject(root);
        if (obj == null) return null;

        ApiResponse<T> api = new ApiResponse<T>();
        api.success = CentralServiceJson.asBoolean(obj.get("success"), false);
        api.errorCode = CentralServiceJson.asIntNullable(obj.get("errorCode"));
        api.errorMessage = CentralServiceJson.asString(obj.get("errorMessage"));
        api.data = dataParser == null ? null : dataParser.parse(obj.get("data"));
        return api;
    }

    private static boolean isSuccess(int statusCode) {
        return statusCode >= 200 && statusCode <= 299;
    }
}
