package erp.centralservice.client;

import erp.centralservice.client.errors.CentralServiceException;
import erp.centralservice.client.internal.CentralServiceErrorParser;
import erp.centralservice.client.internal.CentralServiceHttpClient;
import erp.centralservice.client.internal.CentralServiceJson;
import erp.centralservice.client.models.ApiResponse;
import erp.centralservice.client.models.ServiceInfo;
import erp.centralservice.client.models.ServiceListResponse;
import erp.centralservice.client.models.ServiceNetworkStatus;

import java.util.Map;

/**
 * 面向服务发现侧的 Central Service SDK 客户端。
 */
public final class CentralServiceDiscoveryClient {
    private final CentralServiceHttpClient http;

    public CentralServiceDiscoveryClient(String baseUrl) {
        this(new CentralServiceSdkOptions(baseUrl));
    }

    public CentralServiceDiscoveryClient(CentralServiceSdkOptions options) {
        if (options == null) throw new IllegalArgumentException("options is required");
        this.http = new CentralServiceHttpClient(options);
    }

    public ServiceListResponse list(String name) {
        String path = "/api/Service/list";
        if (name != null && !name.trim().isEmpty()) {
            path += "?name=" + CentralServiceHttpClient.encodeQueryParam(name);
        }

        CentralServiceHttpClient.Response response = send("GET", path, null);
        ApiResponse<ServiceListResponse> api = parseApiResponse(response.body, new ApiResponseDataParser<ServiceListResponse>() {
            @Override
            public ServiceListResponse parse(Object data) {
                return ServiceListResponse.fromJson(data);
            }
        });

        if (api == null) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.unknown("GET", response.url, response.statusCode, response.body, "无法解析响应"),
                    response));
        }
        if (!api.success) {
            throw new CentralServiceException(
                CentralServiceHttpClient.appendTransportContext(
                    CentralServiceErrorParser.parse("GET", response.url, response.statusCode, response.body),
                    response));
        }
        return api.data;
    }

    public ServiceInfo discoverRoundRobin(String serviceName) {
        return getServiceInfo("/api/ServiceDiscovery/discover/roundrobin/" + CentralServiceHttpClient.encodePathSegment(serviceName));
    }

    public ServiceInfo discoverWeighted(String serviceName) {
        return getServiceInfo("/api/ServiceDiscovery/discover/weighted/" + CentralServiceHttpClient.encodePathSegment(serviceName));
    }

    public ServiceInfo discoverBest(String serviceName) {
        return getServiceInfo("/api/ServiceDiscovery/discover/best/" + CentralServiceHttpClient.encodePathSegment(serviceName));
    }

    public ServiceNetworkStatus[] getNetworkAll() {
        return ServiceNetworkStatus.fromJsonArray(send("GET", "/api/ServiceDiscovery/network/all", null).body);
    }

    public ServiceNetworkStatus getNetwork(String serviceId) {
        return ServiceNetworkStatus.fromJson(send("GET", "/api/ServiceDiscovery/network/" + CentralServiceHttpClient.encodePathSegment(serviceId), null).body);
    }

    public ServiceNetworkStatus evaluateNetwork(String serviceId) {
        return ServiceNetworkStatus.fromJson(send("POST", "/api/ServiceDiscovery/network/evaluate/" + CentralServiceHttpClient.encodePathSegment(serviceId), null).body);
    }

    private ServiceInfo getServiceInfo(String path) {
        return ServiceInfo.fromJson(send("GET", path, null).body);
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
