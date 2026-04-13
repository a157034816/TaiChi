package erp.centralservice.service.models;

import erp.centralservice.service.internal.CentralServiceJson;

import java.util.List;
import java.util.Map;

/**
 * 中心服务返回的服务实例信息。
 */
public final class ServiceInfo {
    /** 服务实例唯一标识。 */
    public String id;
    /** 服务名称。 */
    public String name;
    /** 服务主机地址。 */
    public String host;
    /** 服务端口。 */
    public int port;
    /** 服务完整访问地址。 */
    public String url;
    /** 服务类型。 */
    public String serviceType;
    /** 服务状态码。 */
    public int status;
    /** 健康检查地址。 */
    public String healthCheckUrl;
    /** 健康检查端口。 */
    public int healthCheckPort;
    /** 注册时间。 */
    public String registerTime;
    /** 最近一次心跳时间。 */
    public String lastHeartbeatTime;
    /** 服务权重。 */
    public int weight;
    /** 服务元数据键值对。 */
    public Map<String, String> metadata;
    /** 当前实例是否处于本地网络。 */
    public boolean isLocalNetwork;

    /**
     * 从 JSON 文本解析服务信息。
     *
     * @param json JSON 文本
     * @return 解析结果；输入为空白时返回 {@code null}
     */
    public static ServiceInfo fromJson(String json) {
        if (json == null || json.trim().isEmpty()) return null;
        return fromJson(CentralServiceJson.parse(json));
    }

    /**
     * 从 JSON 对象节点解析服务信息。
     *
     * @param v JSON 对象节点
     * @return 解析结果；输入不是对象时返回 {@code null}
     */
    public static ServiceInfo fromJson(Object v) {
        Map<String, Object> m = CentralServiceJson.asObject(v);
        if (m == null) return null;
        ServiceInfo s = new ServiceInfo();
        s.id = CentralServiceJson.asString(m.get("id"));
        s.name = CentralServiceJson.asString(m.get("name"));
        s.host = CentralServiceJson.asString(m.get("host"));
        Integer port = CentralServiceJson.asIntNullable(m.get("port"));
        s.port = port != null ? port : 0;
        s.url = CentralServiceJson.asString(m.get("url"));
        s.serviceType = CentralServiceJson.asString(m.get("serviceType"));
        Integer status = CentralServiceJson.asIntNullable(m.get("status"));
        s.status = status != null ? status : 0;
        s.healthCheckUrl = CentralServiceJson.asString(m.get("healthCheckUrl"));
        Integer hcp = CentralServiceJson.asIntNullable(m.get("healthCheckPort"));
        s.healthCheckPort = hcp != null ? hcp : 0;
        s.registerTime = CentralServiceJson.asString(m.get("registerTime"));
        s.lastHeartbeatTime = CentralServiceJson.asString(m.get("lastHeartbeatTime"));
        Integer w = CentralServiceJson.asIntNullable(m.get("weight"));
        s.weight = w != null ? w : 0;
        s.metadata = CentralServiceJson.asStringMap(m.get("metadata"));
        s.isLocalNetwork = CentralServiceJson.asBoolean(m.get("isLocalNetwork"), false);
        return s;
    }

    /**
     * 从 JSON 数组节点解析多个服务信息。
     *
     * @param v JSON 数组节点
     * @return 服务信息数组；输入不是数组时返回空数组
     */
    public static ServiceInfo[] fromJsonArray(Object v) {
        List<Object> arr = CentralServiceJson.asArray(v);
        if (arr == null) return new ServiceInfo[0];
        ServiceInfo[] out = new ServiceInfo[arr.size()];
        for (int i = 0; i < arr.size(); i++) {
            out[i] = fromJson(arr.get(i));
        }
        return out;
    }
}


