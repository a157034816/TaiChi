use crate::error::{Result, SdkError};
use std::collections::HashMap;
use std::io::{BufRead, BufReader, Read, Write};
use std::net::TcpStream;
use std::time::Duration;

/// 基于 `TcpStream` 的极简 HTTP/1.1 客户端。
///
/// 该实现只覆盖当前 SDK 所需的明文 `http://` 请求能力，不支持 TLS、
/// 代理、重定向、连接复用或压缩解码。
#[derive(Debug, Clone)]
pub struct HttpClient {
    host: String,
    port: u16,
    base_path: String,
    timeout: Duration,
}

/// 原始 HTTP 响应。
#[derive(Debug, Clone)]
pub struct HttpResponse {
    /// HTTP 状态码。
    pub status: u16,
    /// 归一化为小写键的响应头。
    #[allow(dead_code)]
    pub headers: HashMap<String, String>,
    /// 响应体原始字节。
    pub body: Vec<u8>,
}

impl HttpClient {
    /// 由服务端基地址创建 HTTP 客户端。
    ///
    /// `base_url` 仅支持 `http://`，并允许包含固定路径前缀。
    pub fn new(base_url: &str) -> Result<Self> {
        let url = base_url.trim();
        // 当前实现只支持明文 HTTP，保持与现有无额外依赖的传输栈一致。
        let rest = url
            .strip_prefix("http://")
            .ok_or_else(|| SdkError::InvalidUrl("Only http:// is supported".to_string()))?;

        let (authority, path_part) = match rest.find('/') {
            Some(idx) => (&rest[..idx], &rest[idx..]),
            None => (rest, ""),
        };

        if authority.is_empty() {
            return Err(SdkError::InvalidUrl("Missing host".to_string()));
        }

        let (host, port) = parse_host_port(authority)?;

        let base_path = normalize_base_path(path_part);
        Ok(Self {
            host,
            port,
            base_path,
            timeout: Duration::from_secs(10),
        })
    }

    /// 设置请求超时时间。
    pub fn with_timeout(mut self, timeout: Duration) -> Self {
        self.timeout = timeout;
        self
    }

    /// 发送一次 HTTP 请求并返回原始响应。
    ///
    /// `path_and_query` 必须以 `/` 开头；`body` 与 `content_type` 可同时为空。
    pub fn request(
        &self,
        method: &str,
        path_and_query: &str,
        body: Option<&[u8]>,
        content_type: Option<&str>,
        ) -> Result<HttpResponse> {
        let full_path = self.join_path(path_and_query)?;
        let mut stream = TcpStream::connect((&self.host[..], self.port))?;
        // 超时设置失败时保留系统默认值，避免因平台差异阻断基本请求。
        stream.set_read_timeout(Some(self.timeout)).ok();
        stream.set_write_timeout(Some(self.timeout)).ok();

        let host_header = if self.port == 80 {
            self.host.clone()
        } else {
            format!("{}:{}", self.host, self.port)
        };

        // 通过 `Connection: close` 简化响应结束判定，不实现 keep-alive 复用。
        let mut req: Vec<u8> = Vec::new();
        req.extend_from_slice(format!("{method} {full_path} HTTP/1.1\r\n").as_bytes());
        req.extend_from_slice(format!("Host: {host_header}\r\n").as_bytes());
        req.extend_from_slice(b"User-Agent: centralservice-rust/0.1\r\n");
        req.extend_from_slice(b"Accept: application/json\r\n");
        req.extend_from_slice(b"Connection: close\r\n");

        if let Some(b) = body {
            req.extend_from_slice(format!("Content-Length: {}\r\n", b.len()).as_bytes());
            if let Some(ct) = content_type {
                req.extend_from_slice(format!("Content-Type: {ct}\r\n").as_bytes());
            }
        }

        req.extend_from_slice(b"\r\n");
        if let Some(b) = body {
            req.extend_from_slice(b);
        }

        stream.write_all(&req)?;
        stream.flush()?;

        let mut reader = BufReader::new(stream);
        read_response(&mut reader, method)
    }

    fn join_path(&self, path_and_query: &str) -> Result<String> {
        if !path_and_query.starts_with('/') {
            return Err(SdkError::InvalidUrl(format!(
                "Path must start with '/': {path_and_query}"
            )));
        }
        if self.base_path.is_empty() {
            return Ok(path_and_query.to_string());
        }
        if path_and_query == "/" {
            return Ok(self.base_path.clone());
        }
        Ok(format!("{}{}", self.base_path, path_and_query))
    }
}

/// 对路径片段执行基于 UTF-8 字节的百分号编码。
///
/// 仅保留 RFC 3986 非保留字符，其余字节都会编码为 `%XX`。
pub fn url_encode_component(value: &str) -> String {
    let mut out = String::new();
    for &b in value.as_bytes() {
        match b {
            b'A'..=b'Z'
            | b'a'..=b'z'
            | b'0'..=b'9'
            | b'-'
            | b'.'
            | b'_'
            | b'~' => out.push(b as char),
            _ => out.push_str(&format!("%{:02X}", b)),
        }
    }
    out
}

fn parse_host_port(authority: &str) -> Result<(String, u16)> {
    let authority = authority.trim();
    if authority.starts_with('[') {
        // 仅覆盖最小 IPv6 `[::1]:5000` 形式，不支持更复杂的 authority 变体。
        let end = authority
            .find(']')
            .ok_or_else(|| SdkError::InvalidUrl("Invalid IPv6 host".to_string()))?;
        let host = &authority[1..end];
        let port = if authority.len() > end + 1 {
            let rest = &authority[end + 1..];
            let p = rest.strip_prefix(':').ok_or_else(|| {
                SdkError::InvalidUrl("Invalid IPv6 host:port".to_string())
            })?;
            p.parse::<u16>()
                .map_err(|_| SdkError::InvalidUrl("Invalid port".to_string()))?
        } else {
            80
        };
        return Ok((host.to_string(), port));
    }

    match authority.rsplit_once(':') {
        Some((host, port_str)) if !host.is_empty() && !port_str.is_empty() => {
            let port = port_str
                .parse::<u16>()
                .map_err(|_| SdkError::InvalidUrl("Invalid port".to_string()))?;
            Ok((host.to_string(), port))
        }
        _ => Ok((authority.to_string(), 80)),
    }
}

fn normalize_base_path(path: &str) -> String {
    let p = path.trim();
    if p.is_empty() || p == "/" {
        return String::new();
    }
    let mut out = p.to_string();
    while out.ends_with('/') {
        out.pop();
    }
    out
}

fn read_response(reader: &mut BufReader<TcpStream>, method: &str) -> Result<HttpResponse> {
    let status_line = read_crlf_line(reader)?.ok_or_else(|| SdkError::HttpStatus {
        status: 0,
        message: "Empty response".to_string(),
        body: None,
    })?;
    let mut parts = status_line.split_whitespace();
    let _http = parts.next().unwrap_or("");
    let status_str = parts.next().unwrap_or("");
    let status: u16 = status_str.parse().map_err(|_| SdkError::HttpStatus {
        status: 0,
        message: format!("Invalid status line: {status_line}"),
        body: None,
    })?;

    let mut headers: HashMap<String, String> = HashMap::new();
    loop {
        let line = read_crlf_line(reader)?.ok_or_else(|| SdkError::HttpStatus {
            status,
            message: "Unexpected EOF in headers".to_string(),
            body: None,
        })?;
        if line.is_empty() {
            break;
        }
        if let Some((k, v)) = line.split_once(':') {
            let key = k.trim().to_ascii_lowercase();
            let val = v.trim().to_string();
            headers
                .entry(key)
                .and_modify(|e| {
                    e.push_str(", ");
                    e.push_str(&val);
                })
                .or_insert(val);
        }
    }

    // HEAD/204/304 按无消息体语义处理，即使服务端附带长度头也直接忽略。
    if method.eq_ignore_ascii_case("HEAD") || status == 204 || status == 304 {
        return Ok(HttpResponse {
            status,
            headers,
            body: Vec::new(),
        });
    }

    let body = if headers
        .get("transfer-encoding")
        .map(|v| v.to_ascii_lowercase().contains("chunked"))
        .unwrap_or(false)
    {
        read_chunked_body(reader)?
    } else if let Some(len) = headers.get("content-length") {
        let n: usize = len.trim().parse().map_err(|_| SdkError::HttpStatus {
            status,
            message: "Invalid Content-Length".to_string(),
            body: None,
        })?;
        read_exact_bytes(reader, n)?
    } else {
        // 未提供长度时退回读取到 EOF；这依赖请求端显式声明 `Connection: close`。
        let mut buf = Vec::new();
        reader.read_to_end(&mut buf)?;
        buf
    };

    Ok(HttpResponse {
        status,
        headers,
        body,
    })
}

fn read_crlf_line<R: BufRead>(reader: &mut R) -> Result<Option<String>> {
    let mut buf: Vec<u8> = Vec::new();
    let n = reader.read_until(b'\n', &mut buf)?;
    if n == 0 {
        return Ok(None);
    }
    if buf.ends_with(b"\n") {
        buf.pop();
    }
    if buf.ends_with(b"\r") {
        buf.pop();
    }
    Ok(Some(String::from_utf8_lossy(&buf).to_string()))
}

fn read_exact_bytes<R: Read>(reader: &mut R, n: usize) -> Result<Vec<u8>> {
    let mut buf = vec![0u8; n];
    reader.read_exact(&mut buf)?;
    Ok(buf)
}

fn read_chunked_body(reader: &mut BufReader<TcpStream>) -> Result<Vec<u8>> {
    let mut out: Vec<u8> = Vec::new();
    loop {
        let line = read_crlf_line(reader)?.ok_or_else(|| SdkError::HttpStatus {
            status: 0,
            message: "Unexpected EOF in chunked body".to_string(),
            body: None,
        })?;
        let size_str = line.split(';').next().unwrap_or("").trim();
        let size = usize::from_str_radix(size_str, 16).map_err(|_| SdkError::HttpStatus {
            status: 0,
            message: format!("Invalid chunk size: {line}"),
            body: None,
        })?;
        if size == 0 {
            // trailer 只负责消费掉，当前 SDK 不向上层暴露 trailer 语义。
            loop {
                let trailer = read_crlf_line(reader)?.ok_or_else(|| SdkError::HttpStatus {
                    status: 0,
                    message: "Unexpected EOF in trailers".to_string(),
                    body: None,
                })?;
                if trailer.is_empty() {
                    break;
                }
            }
            break;
        }
        let mut chunk = vec![0u8; size];
        reader.read_exact(&mut chunk)?;
        out.extend_from_slice(&chunk);

        let mut crlf = [0u8; 2];
        reader.read_exact(&mut crlf)?;
        if &crlf != b"\r\n" {
            return Err(SdkError::HttpStatus {
                status: 0,
                message: "Invalid chunk terminator".to_string(),
                body: None,
            });
        }
    }
    Ok(out)
}


