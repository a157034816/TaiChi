package erp.centralservice.service.errors;

/**
 * 表示中心服务 SDK 调用失败的运行时异常。
 *
 * <p>异常主体是 {@link CentralServiceError}，调用方通常应优先读取
 * {@link #getError()} 获取结构化错误上下文，而不是只依赖异常消息文本。</p>
 */
public final class CentralServiceException extends RuntimeException {
    /** 结构化错误详情。 */
    private final CentralServiceError error;

    /**
     * 使用结构化错误详情构造异常。
     *
     * @param error 错误详情，可为 {@code null}
     */
    public CentralServiceException(CentralServiceError error) {
        super(error != null ? error.message : "CentralServiceException");
        this.error = error;
    }

    /**
     * 返回结构化错误详情。
     *
     * @return 错误详情；如果构造时未提供则为 {@code null}
     */
    public CentralServiceError getError() {
        return error;
    }

    /**
     * 优先输出结构化错误的摘要文本。
     *
     * @return 单行错误描述
     */
    @Override
    public String toString() {
        return error != null ? error.toString() : super.toString();
    }
}


