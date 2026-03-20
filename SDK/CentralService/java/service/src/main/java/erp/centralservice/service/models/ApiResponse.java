package erp.centralservice.service.models;

/**
 * 与中心服务 HTTP API 对齐的通用响应包裹对象。
 *
 * @param <T> 业务数据节点类型
 */
public final class ApiResponse<T> {
    /** 业务操作是否成功。 */
    public boolean success;
    /** 失败时的业务错误码；成功或缺失时为 {@code null}。 */
    public Integer errorCode;
    /** 失败时的错误消息。 */
    public String errorMessage;
    /** 业务数据负载。 */
    public T data;
}


