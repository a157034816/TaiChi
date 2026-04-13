package erp.centralservice.client.models;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * 服务注册请求模型。
 *
 * <p>字段名称与服务端 JSON 协议保持一一对应，新增字段前需要同时更新服务端契约。</p>
 */
public final class ServiceRegistrationRequest {
    /** 可选的服务 ID；为空时通常由服务端生成。 */
    public String id;
    /** 服务名称。 */
    public String name;
    /** 服务主机地址。 */
    public String host;
    /** 服务端口。 */
    public int port;
    /** 服务类型。 */
    public String serviceType;
    /** 健康检查地址。 */
    public String healthCheckUrl;
    /** 健康检查端口。 */
    public int healthCheckPort;
    /** 路由或调度权重。 */
    public int weight;
    /** 扩展元数据。 */
    public Map<String, String> metadata;

    /**
     * 转换为与中心服务接口兼容的 JSON 对象。
     *
     * @return 可直接序列化的键值映射
     */
    public Map<String, Object> toJson() {
        Map<String, Object> m = new LinkedHashMap<String, Object>();
        m.put("id", id);
        m.put("name", name);
        m.put("host", host);
        m.put("port", port);
        m.put("serviceType", serviceType);
        m.put("healthCheckUrl", healthCheckUrl);
        m.put("healthCheckPort", healthCheckPort);
        m.put("weight", weight);
        m.put("metadata", metadata);
        return m;
    }
}
