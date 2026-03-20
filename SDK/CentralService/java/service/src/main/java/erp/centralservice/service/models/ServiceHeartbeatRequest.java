package erp.centralservice.service.models;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * 服务心跳请求模型。
 */
public final class ServiceHeartbeatRequest {
    /** 已注册服务实例的唯一标识。 */
    public String id;

    /**
     * 转换为与中心服务接口兼容的 JSON 对象。
     *
     * @return 可直接序列化的键值映射
     */
    public Map<String, Object> toJson() {
        Map<String, Object> m = new LinkedHashMap<String, Object>();
        m.put("id", id);
        return m;
    }
}


