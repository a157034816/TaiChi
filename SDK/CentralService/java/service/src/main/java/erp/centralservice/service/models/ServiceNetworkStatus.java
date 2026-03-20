package erp.centralservice.service.models;

import erp.centralservice.service.internal.CentralServiceJson;

import java.util.List;
import java.util.Map;

/**
 * 服务网络探测结果。
 */
public final class ServiceNetworkStatus {
    /** 对应的服务实例 ID。 */
    public String serviceId;
    /** 最近一次探测的响应耗时，单位毫秒。 */
    public long responseTime;
    /** 最近一次探测的丢包率，通常使用百分比表示。 */
    public double packetLoss;
    /** 最近一次网络检查时间。 */
    public String lastCheckTime;
    /** 连续成功次数。 */
    public int consecutiveSuccesses;
    /** 连续失败次数。 */
    public int consecutiveFailures;
    /** 当前是否可用。 */
    public boolean isAvailable;

    /**
     * 根据响应时间与丢包率计算一个 0 到 100 的经验分数。
     *
     * <p>边界条件：当 {@link #isAvailable} 为 {@code false} 时直接返回 0；
     * 响应时间大于等于 1000ms 或丢包率大于等于 50 时，对应维度分数会降为 0。</p>
     *
     * @return 经验评分
     */
    public int calculateScore() {
        if (!isAvailable) return 0;

        int responseTimeScore;
        if (responseTime <= 50) {
            responseTimeScore = 50;
        } else if (responseTime >= 1000) {
            responseTimeScore = 0;
        } else {
            responseTimeScore = (int) Math.floor(50.0 * (1.0 - (double) (responseTime - 50) / 950.0));
        }

        int packetLossScore;
        if (packetLoss <= 0) {
            packetLossScore = 50;
        } else if (packetLoss >= 50) {
            packetLossScore = 0;
        } else {
            packetLossScore = (int) Math.floor(50.0 * (1.0 - packetLoss / 50.0));
        }

        return responseTimeScore + packetLossScore;
    }

    /**
     * 从 JSON 文本解析网络状态。
     *
     * @param json JSON 文本
     * @return 解析结果；输入为空白时返回 {@code null}
     */
    public static ServiceNetworkStatus fromJson(String json) {
        if (json == null || json.trim().isEmpty()) return null;
        return fromJson(CentralServiceJson.parse(json));
    }

    /**
     * 从 JSON 对象节点解析网络状态。
     *
     * @param v JSON 对象节点
     * @return 解析结果；输入不是对象时返回 {@code null}
     */
    public static ServiceNetworkStatus fromJson(Object v) {
        Map<String, Object> m = CentralServiceJson.asObject(v);
        if (m == null) return null;
        ServiceNetworkStatus s = new ServiceNetworkStatus();
        s.serviceId = CentralServiceJson.asString(m.get("serviceId"));
        Long rt = CentralServiceJson.asLongNullable(m.get("responseTime"));
        s.responseTime = rt != null ? rt : 0L;
        Double pl = CentralServiceJson.asDoubleNullable(m.get("packetLoss"));
        s.packetLoss = pl != null ? pl : 0.0;
        s.lastCheckTime = CentralServiceJson.asString(m.get("lastCheckTime"));
        Integer cs = CentralServiceJson.asIntNullable(m.get("consecutiveSuccesses"));
        s.consecutiveSuccesses = cs != null ? cs : 0;
        Integer cf = CentralServiceJson.asIntNullable(m.get("consecutiveFailures"));
        s.consecutiveFailures = cf != null ? cf : 0;
        s.isAvailable = CentralServiceJson.asBoolean(m.get("isAvailable"), false);
        return s;
    }

    /**
     * 从 JSON 文本解析网络状态数组。
     *
     * @param json JSON 文本
     * @return 网络状态数组；输入为空白时返回空数组
     */
    public static ServiceNetworkStatus[] fromJsonArray(String json) {
        if (json == null || json.trim().isEmpty()) return new ServiceNetworkStatus[0];
        Object root = CentralServiceJson.parse(json);
        return fromJsonArray(root);
    }

    /**
     * 从 JSON 数组节点解析网络状态数组。
     *
     * @param v JSON 数组节点
     * @return 网络状态数组；输入不是数组时返回空数组
     */
    public static ServiceNetworkStatus[] fromJsonArray(Object v) {
        List<Object> arr = CentralServiceJson.asArray(v);
        if (arr == null) return new ServiceNetworkStatus[0];
        ServiceNetworkStatus[] out = new ServiceNetworkStatus[arr.size()];
        for (int i = 0; i < arr.size(); i++) {
            out[i] = fromJson(arr.get(i));
        }
        return out;
    }
}


