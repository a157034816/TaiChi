package erp.centralservice.service.models;

import erp.centralservice.service.internal.CentralServiceJson;

import java.util.Map;

/**
 * 服务列表查询结果。
 */
public final class ServiceListResponse {
    /** 服务列表；当响应体未提供该字段时通常为空数组。 */
    public ServiceInfo[] services;

    /**
     * 从 JSON 对象节点解析列表响应。
     *
     * @param v JSON 对象节点
     * @return 解析结果；输入不是对象时返回 {@code null}
     */
    public static ServiceListResponse fromJson(Object v) {
        Map<String, Object> m = CentralServiceJson.asObject(v);
        if (m == null) return null;
        ServiceListResponse r = new ServiceListResponse();
        r.services = ServiceInfo.fromJsonArray(m.get("services"));
        return r;
    }
}


