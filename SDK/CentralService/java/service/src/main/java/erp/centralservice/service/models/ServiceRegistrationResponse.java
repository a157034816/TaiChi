package erp.centralservice.service.models;

import erp.centralservice.service.internal.CentralServiceJson;

import java.util.Map;

/**
 * 服务注册成功后的返回模型。
 */
public final class ServiceRegistrationResponse {
    /** 最终生效的服务实例 ID。 */
    public String id;
    /** 注册时间戳。 */
    public long registerTimestamp;

    /**
     * 从 JSON 对象节点解析注册结果。
     *
     * @param v JSON 对象节点
     * @return 解析结果；输入不是对象时返回 {@code null}
     */
    public static ServiceRegistrationResponse fromJson(Object v) {
        Map<String, Object> m = CentralServiceJson.asObject(v);
        if (m == null) return null;
        ServiceRegistrationResponse r = new ServiceRegistrationResponse();
        r.id = CentralServiceJson.asString(m.get("id"));
        Long ts = CentralServiceJson.asLongNullable(m.get("registerTimestamp"));
        r.registerTimestamp = ts != null ? ts : 0L;
        return r;
    }
}


