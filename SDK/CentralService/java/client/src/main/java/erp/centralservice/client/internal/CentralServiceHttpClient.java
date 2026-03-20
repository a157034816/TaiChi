package erp.centralservice.client.internal;

import erp.centralservice.client.CentralServiceSdkOptions;
import erp.centralservice.client.errors.CentralServiceError;
import erp.centralservice.client.errors.CentralServiceErrorKind;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.ConnectException;
import java.net.HttpURLConnection;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * 基于 {@link HttpURLConnection} 的多端点 HTTP 传输层。
 */
public final class CentralServiceHttpClient {
    private final List<EndpointRuntime> endpoints;
    private final int timeoutMs;
    private final String userAgent;

    public CentralServiceHttpClient(CentralServiceSdkOptions options) {
        if (options == null) throw new IllegalArgumentException("options is required");
        this.endpoints = new ArrayList<EndpointRuntime>(options.endpoints.size());
        for (int i = 0; i < options.endpoints.size(); i++) {
            CentralServiceSdkOptions.CentralServiceEndpointOptions endpoint = options.endpoints.get(i);
            endpoints.add(new EndpointRuntime(endpoint));
        }
        this.timeoutMs = options.timeoutMs > 0 ? options.timeoutMs : 5000;
        this.userAgent = options.userAgent;
    }

    public Response send(String method, String path, String body) {
        ArrayList<String> skippedEndpoints = new ArrayList<String>();
        ArrayList<String> failureSummaries = new ArrayList<String>();
        String lastUrl = null;

        for (int i = 0; i < endpoints.size(); i++) {
            EndpointRuntime endpoint = endpoints.get(i);
            String skipReason = endpoint.circuitBreaker == null ? null : endpoint.circuitBreaker.tryAllowRequest(System.currentTimeMillis());
            if (skipReason != null) {
                skippedEndpoints.add(endpoint.baseUrl + "（" + skipReason + "）");
                continue;
            }

            for (int attempt = 1; attempt <= endpoint.maxAttempts; attempt++) {
                String url = buildUrl(endpoint.baseUrl, path);
                lastUrl = url;
                try {
                    Response response = sendCore(method, url, body);
                    if (endpoint.circuitBreaker != null) {
                        endpoint.circuitBreaker.reportSuccess();
                    }
                    return new Response(
                        endpoint.baseUrl,
                        url,
                        attempt,
                        endpoint.maxAttempts,
                        response.statusCode,
                        response.body,
                        skippedEndpoints);
                } catch (IOException ex) {
                    if (!isTransportException(ex)) throw new RuntimeException(ex);
                    if (endpoint.circuitBreaker != null) {
                        endpoint.circuitBreaker.reportFailure(System.currentTimeMillis());
                    }
                    failureSummaries.add(
                        endpoint.baseUrl + " 第 " + attempt + "/" + endpoint.maxAttempts + " 次失败：" +
                            ex.getClass().getSimpleName() + ": " + ex.getMessage());
                }
            }
        }

        ArrayList<String> segments = new ArrayList<String>();
        if (!skippedEndpoints.isEmpty()) {
            segments.add("跳过端点: " + join("; ", skippedEndpoints));
        }
        if (!failureSummaries.isEmpty()) {
            segments.add("失败详情: " + join("; ", failureSummaries));
        }
        if (segments.isEmpty()) {
            segments.add("未找到可用的中心服务端点。");
        }
        String summary = join(" | ", segments);
        throw new RuntimeException(new TransportExhaustedException(method, path, lastUrl, summary));
    }

    public static CentralServiceError createTransportError(String method, String path, RuntimeException ex) {
        Throwable cause = ex.getCause();
        if (cause instanceof TransportExhaustedException) {
            TransportExhaustedException transport = (TransportExhaustedException) cause;
            String target = transport.lastUrl == null || transport.lastUrl.isEmpty() ? path : transport.lastUrl;
            return new CentralServiceError(
                503,
                method,
                target,
                CentralServiceErrorKind.Transport,
                "中心服务调用失败，所有可用端点均已耗尽。 " + transport.rawDetail,
                null,
                transport.rawDetail);
        }
        return new CentralServiceError(503, method, path, CentralServiceErrorKind.Transport, ex.getMessage(), null, null);
    }

    public static CentralServiceError appendTransportContext(CentralServiceError error, Response response) {
        if (error == null) return null;
        ArrayList<String> segments = new ArrayList<String>();
        segments.add("端点=" + response.baseUrl);
        segments.add("尝试=" + response.attempt + "/" + response.maxAttempts);
        if (!response.skippedEndpoints.isEmpty()) {
            segments.add("已跳过=" + join("、", response.skippedEndpoints));
        }
        return new CentralServiceError(
            error.httpStatus,
            error.method,
            error.url,
            error.kind,
            error.message + " (" + join("; ", segments) + ")",
            error.errorCode,
            error.rawBody);
    }

    public static String buildUrl(String baseUrl, String path) {
        String p = path == null ? "" : path.trim();
        if (!p.startsWith("/")) p = "/" + p;
        return baseUrl + p;
    }

    public static String encodeQueryParam(String value) {
        if (value == null) return "";
        try {
            return URLEncoder.encode(value, "UTF-8");
        } catch (Exception e) {
            return value;
        }
    }

    public static String encodePathSegment(String value) {
        if (value == null) return "";
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        StringBuilder sb = new StringBuilder(bytes.length);
        for (int i = 0; i < bytes.length; i++) {
            int b = bytes[i] & 0xFF;
            char c = (char) b;
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '_' || c == '~') {
                sb.append(c);
            } else {
                sb.append('%');
                char hi = Character.toUpperCase(Character.forDigit((b >> 4) & 0xF, 16));
                char lo = Character.toUpperCase(Character.forDigit(b & 0xF, 16));
                sb.append(hi).append(lo);
            }
        }
        return sb.toString();
    }

    private Response sendCore(String method, String url, String body) throws IOException {
        HttpURLConnection conn = (HttpURLConnection) new URL(url).openConnection();
        conn.setRequestMethod(method);
        conn.setConnectTimeout(timeoutMs);
        conn.setReadTimeout(timeoutMs);
        conn.setRequestProperty("Accept", "application/json");
        if (userAgent != null && !userAgent.trim().isEmpty()) {
            conn.setRequestProperty("User-Agent", userAgent);
        }

        if (body != null) {
            byte[] payload = body.getBytes(StandardCharsets.UTF_8);
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            conn.setFixedLengthStreamingMode(payload.length);
            OutputStream os = null;
            try {
                os = conn.getOutputStream();
                os.write(payload);
                os.flush();
            } finally {
                if (os != null) os.close();
            }
        }

        int status = conn.getResponseCode();
        InputStream is = null;
        try {
            is = (status >= 200 && status <= 299) ? conn.getInputStream() : conn.getErrorStream();
            return new Response(null, url, 0, 0, status, readAllUtf8(is), Collections.<String>emptyList());
        } finally {
            if (is != null) is.close();
            conn.disconnect();
        }
    }

    private static String readAllUtf8(InputStream is) throws IOException {
        if (is == null) return "";
        ByteArrayOutputStream baos = new ByteArrayOutputStream();
        byte[] buf = new byte[4096];
        int n;
        while ((n = is.read(buf)) >= 0) {
            baos.write(buf, 0, n);
        }
        return new String(baos.toByteArray(), StandardCharsets.UTF_8);
    }

    private static boolean isTransportException(IOException ex) {
        return ex instanceof SocketTimeoutException
            || ex instanceof ConnectException
            || ex instanceof java.net.NoRouteToHostException
            || ex instanceof java.net.UnknownHostException
            || ex instanceof java.net.SocketException;
    }

    private static String join(String separator, List<String> values) {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < values.size(); i++) {
            if (i > 0) builder.append(separator);
            builder.append(values.get(i));
        }
        return builder.toString();
    }

    public static final class Response {
        public final String baseUrl;
        public final String url;
        public final int attempt;
        public final int maxAttempts;
        public final int statusCode;
        public final String body;
        public final List<String> skippedEndpoints;

        public Response(String baseUrl, String url, int attempt, int maxAttempts, int statusCode, String body, List<String> skippedEndpoints) {
            this.baseUrl = baseUrl == null ? "" : baseUrl;
            this.url = url == null ? "" : url;
            this.attempt = attempt;
            this.maxAttempts = maxAttempts;
            this.statusCode = statusCode;
            this.body = body == null ? "" : body;
            this.skippedEndpoints = skippedEndpoints == null
                ? Collections.<String>emptyList()
                : Collections.unmodifiableList(new ArrayList<String>(skippedEndpoints));
        }
    }

    private static final class EndpointRuntime {
        final String baseUrl;
        final int maxAttempts;
        final CircuitBreakerState circuitBreaker;

        EndpointRuntime(CentralServiceSdkOptions.CentralServiceEndpointOptions options) {
            this.baseUrl = options.baseUrl;
            this.maxAttempts = CentralServiceSdkOptions.normalizeMaxAttempts(options.maxAttempts);
            this.circuitBreaker = options.circuitBreaker == null ? null : new CircuitBreakerState(options.circuitBreaker);
        }
    }

    private static final class CircuitBreakerState {
        private final int failureThreshold;
        private final long breakDurationMs;
        private final int recoveryThreshold;
        private int failureCount;
        private int halfOpenSuccessCount;
        private long openUntil;
        private int mode;

        CircuitBreakerState(CentralServiceSdkOptions.CentralServiceCircuitBreakerOptions options) {
            this.failureThreshold = Math.max(1, options.failureThreshold);
            this.breakDurationMs = Math.max(1, options.breakDurationMinutes) * 60L * 1000L;
            this.recoveryThreshold = Math.max(1, options.recoveryThreshold);
        }

        synchronized String tryAllowRequest(long now) {
            if (mode == 1) {
                if (now >= openUntil) {
                    mode = 2;
                    failureCount = 0;
                    halfOpenSuccessCount = 0;
                    return null;
                }
                long remainingSeconds = Math.max(1L, (openUntil - now + 999L) / 1000L);
                return "熔断开启，剩余约 " + remainingSeconds + " 秒";
            }
            return null;
        }

        synchronized void reportSuccess() {
            if (mode == 2) {
                halfOpenSuccessCount++;
                if (halfOpenSuccessCount >= recoveryThreshold) {
                    mode = 0;
                    failureCount = 0;
                    halfOpenSuccessCount = 0;
                    openUntil = 0L;
                }
                return;
            }
            failureCount = 0;
        }

        synchronized void reportFailure(long now) {
            if (mode == 2) {
                open(now);
                return;
            }
            failureCount++;
            if (failureCount >= failureThreshold) {
                open(now);
            }
        }

        private void open(long now) {
            mode = 1;
            failureCount = 0;
            halfOpenSuccessCount = 0;
            openUntil = now + breakDurationMs;
        }
    }

    private static final class TransportExhaustedException extends Exception {
        final String method;
        final String path;
        final String lastUrl;
        final String rawDetail;

        TransportExhaustedException(String method, String path, String lastUrl, String rawDetail) {
            super(rawDetail);
            this.method = method;
            this.path = path;
            this.lastUrl = lastUrl;
            this.rawDetail = rawDetail;
        }
    }
}
