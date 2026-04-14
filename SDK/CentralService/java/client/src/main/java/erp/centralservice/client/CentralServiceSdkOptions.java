package erp.centralservice.client;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/**
 * Central Service 发现侧 SDK 的基础配置。
 *
 * <p>配置同时支持单 {@code baseUrl} 构造与多端点 {@code endpoints} 配置。
 * discovery 侧会按优先级顺序请求端点，并在发生传输层异常时按单端点最大尝试次数与熔断状态切换备用。</p>
 */
public final class CentralServiceSdkOptions {
    private static final int DEFAULT_MAX_ATTEMPTS = 2;

    /** 首个中心服务根地址（与 {@link #endpoints} 的首项等价）。 */
    public final String baseUrl;
    /** 按优先级归一化后的中心服务端点列表。 */
    public final List<CentralServiceEndpointOptions> endpoints;
    /** 请求连接与读取超时时间，单位毫秒；小于等于 0 时由 HTTP 层回退为默认值。 */
    public int timeoutMs = 5000;
    /** 发送给服务端的 User-Agent；为空白时不会写入请求头。 */
    public String userAgent = "centralservice-java-client/0.1.0";

    /**
     * 使用中心服务根地址创建配置对象。
     *
     * @param baseUrl 中心服务根地址，例如 {@code http://127.0.0.1:5000}
     */
    public CentralServiceSdkOptions(String baseUrl) {
        this(Collections.singletonList(new CentralServiceEndpointOptions(baseUrl)));
    }

    /**
     * 使用中心服务端点列表创建配置对象。
     *
     * @param endpoints 中心服务端点列表
     */
    public CentralServiceSdkOptions(List<CentralServiceEndpointOptions> endpoints) {
        if (endpoints == null) throw new IllegalArgumentException("endpoints is required");
        this.endpoints = normalizeEndpoints(endpoints);
        if (this.endpoints.isEmpty()) throw new IllegalArgumentException("at least one endpoint is required");
        this.baseUrl = this.endpoints.get(0).baseUrl;
    }

    private static List<CentralServiceEndpointOptions> normalizeEndpoints(List<CentralServiceEndpointOptions> endpoints) {
        ArrayList<CentralServiceEndpointOptions> normalized = new ArrayList<CentralServiceEndpointOptions>();
        for (int i = 0; i < endpoints.size(); i++) {
            CentralServiceEndpointOptions endpoint = endpoints.get(i);
            if (endpoint == null || endpoint.baseUrl == null || endpoint.baseUrl.trim().isEmpty()) {
                continue;
            }
            normalized.add(endpoint.normalize(i));
        }
        Collections.sort(normalized, new Comparator<CentralServiceEndpointOptions>() {
            @Override
            public int compare(CentralServiceEndpointOptions left, CentralServiceEndpointOptions right) {
                if (left.priority != right.priority) {
                    return left.priority < right.priority ? -1 : 1;
                }
                if (left.order == right.order) {
                    return 0;
                }
                return left.order < right.order ? -1 : 1;
            }
        });
        return Collections.unmodifiableList(normalized);
    }

    static String normalizeBaseUrl(String baseUrl) {
        if (baseUrl == null || baseUrl.trim().isEmpty()) throw new IllegalArgumentException("baseUrl is required");
        String s = baseUrl.trim();
        while (s.endsWith("/")) {
            s = s.substring(0, s.length() - 1);
        }
        return s;
    }

    public static int normalizeMaxAttempts(Integer maxAttempts) {
        int value = maxAttempts == null ? DEFAULT_MAX_ATTEMPTS : maxAttempts.intValue();
        return value < 1 ? DEFAULT_MAX_ATTEMPTS : value;
    }

    /**
     * 表示单个中心服务端点的连接与熔断配置。
     */
    public static final class CentralServiceEndpointOptions {
        /** 中心服务根地址。 */
        public final String baseUrl;
        /** 优先级，数值越小越优先。 */
        public int priority;
        /** 单中心最大尝试次数；为空时使用默认值 2。 */
        public Integer maxAttempts;
        /** 单中心熔断器配置；为空表示禁用熔断。 */
        public CentralServiceCircuitBreakerOptions circuitBreaker;
        int order;

        /**
         * 使用中心服务根地址创建端点配置。
         *
         * @param baseUrl 中心服务根地址
         */
        public CentralServiceEndpointOptions(String baseUrl) {
            this.baseUrl = baseUrl == null ? "" : baseUrl;
        }

        CentralServiceEndpointOptions normalize(int endpointOrder) {
            CentralServiceEndpointOptions normalized = new CentralServiceEndpointOptions(normalizeBaseUrl(baseUrl));
            normalized.priority = priority;
            normalized.maxAttempts = Integer.valueOf(normalizeMaxAttempts(maxAttempts));
            normalized.circuitBreaker = circuitBreaker == null ? null : circuitBreaker.normalize();
            normalized.order = endpointOrder;
            return normalized;
        }
    }

    /**
     * 表示单个中心服务端点的熔断配置。
     */
    public static final class CentralServiceCircuitBreakerOptions {
        /** 连续失败阈值。 */
        public int failureThreshold;
        /** 熔断持续时间，单位分钟。 */
        public int breakDurationMinutes;
        /** 半开状态恢复所需的连续成功阈值。 */
        public int recoveryThreshold;

        CentralServiceCircuitBreakerOptions normalize() {
            CentralServiceCircuitBreakerOptions normalized = new CentralServiceCircuitBreakerOptions();
            normalized.failureThreshold = Math.max(1, failureThreshold);
            normalized.breakDurationMinutes = Math.max(1, breakDurationMinutes);
            normalized.recoveryThreshold = Math.max(1, recoveryThreshold);
            return normalized;
        }
    }
}
