use std::fmt;

/// SDK 统一返回类型。
pub type Result<T> = std::result::Result<T, SdkError>;

/// SDK 在 URL 校验、HTTP 传输、JSON 解析和模型转换过程中返回的错误。
#[derive(Debug)]
pub enum SdkError {
    /// 基础 URL、主机或路径不满足当前实现支持范围。
    InvalidUrl(String),
    /// 底层 TCP 连接、读写或超时设置失败。
    Io(std::io::Error),
    /// HTTP 层返回了非预期状态，或响应格式不完整。
    HttpStatus {
        /// 原始 HTTP 状态码；无法解析状态行时为 `0`。
        status: u16,
        /// 对状态异常的文字说明。
        message: String,
        /// 可选的响应体文本，便于排查服务端返回内容。
        body: Option<String>,
    },
    /// JSON 文本解析失败。
    JsonParse(String),
    /// 服务端返回了业务层错误。
    Api {
        /// 可选的业务错误码。
        code: Option<i64>,
        /// 业务错误消息。
        message: String,
    },
    /// 所有候选端点都因传输失败或熔断跳过而无法完成请求。
    Transport {
        /// 面向调用方展示的摘要。
        message: String,
        /// 便于日志排查的额外原始上下文。
        detail: String,
    },
    /// 响应 JSON 结构与 SDK 预期模型不匹配。
    Model(String),
}

impl fmt::Display for SdkError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SdkError::InvalidUrl(v) => write!(f, "InvalidUrl: {v}"),
            SdkError::Io(e) => write!(f, "Io: {e}"),
            SdkError::HttpStatus {
                status,
                message,
                body,
            } => {
                write!(f, "HttpStatus {status}: {message}")?;
                if let Some(b) = body {
                    write!(f, " ({b})")?;
                }
                Ok(())
            }
            SdkError::JsonParse(msg) => write!(f, "JsonParse: {msg}"),
            SdkError::Api { code, message } => match code {
                Some(c) => write!(f, "ApiError {c}: {message}"),
                None => write!(f, "ApiError: {message}"),
            },
            SdkError::Transport { message, detail } => {
                write!(f, "TransportError: {message}")?;
                if !detail.is_empty() {
                    write!(f, " ({detail})")?;
                }
                Ok(())
            }
            SdkError::Model(msg) => write!(f, "ModelError: {msg}"),
        }
    }
}

impl std::error::Error for SdkError {}

impl From<std::io::Error> for SdkError {
    fn from(value: std::io::Error) -> Self {
        SdkError::Io(value)
    }
}
