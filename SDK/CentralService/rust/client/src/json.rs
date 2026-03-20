use crate::error::{Result, SdkError};
use std::collections::BTreeMap;

// 这里保留一个无外部依赖的最小 JSON 实现，覆盖 SDK 当前需要的读写能力。

/// JSON 数字的内部表示。
#[derive(Debug, Clone, PartialEq)]
pub enum JsonNumber {
    /// 64 位整数。
    I64(i64),
    /// 64 位浮点数。
    F64(f64),
}

impl JsonNumber {
    /// 当数字能无损表示为 `i64` 时返回对应值。
    pub fn as_i64(&self) -> Option<i64> {
        match self {
            JsonNumber::I64(v) => Some(*v),
            JsonNumber::F64(v) => {
                if v.is_finite()
                    && v.fract() == 0.0
                    && *v >= i64::MIN as f64
                    && *v <= i64::MAX as f64
                {
                    Some(*v as i64)
                } else {
                    None
                }
            }
        }
    }

    /// 将数字转换为 `f64` 表示。
    pub fn as_f64(&self) -> Option<f64> {
        match self {
            JsonNumber::I64(v) => Some(*v as f64),
            JsonNumber::F64(v) => Some(*v),
        }
    }
}

/// 轻量 JSON 值树。
#[derive(Debug, Clone, PartialEq)]
pub enum JsonValue {
    /// `null`。
    Null,
    /// 布尔值。
    Bool(bool),
    /// 数字值。
    Number(JsonNumber),
    /// 字符串值。
    String(String),
    /// 数组值。
    Array(Vec<JsonValue>),
    /// 对象值。
    Object(BTreeMap<String, JsonValue>),
}

impl JsonValue {
    /// 从完整 JSON 文本解析一个值。
    ///
    /// 解析成功后要求输入被完全消费，尾随字符会被视为错误。
    pub fn parse(input: &str) -> Result<JsonValue> {
        let mut parser = Parser::new(input.as_bytes());
        let value = parser.parse_value()?;
        parser.skip_ws();
        // 必须消费完整输入，避免静默忽略尾随脏数据。
        if !parser.eof() {
            return Err(SdkError::JsonParse(format!(
                "Trailing characters at byte {}",
                parser.idx
            )));
        }
        Ok(value)
    }

    /// 将当前值序列化为 JSON 文本。
    pub fn to_string(&self) -> String {
        let mut out = String::new();
        self.write_to(&mut out);
        out
    }

    /// 若当前值是对象，则返回对象引用。
    pub fn as_object(&self) -> Option<&BTreeMap<String, JsonValue>> {
        match self {
            JsonValue::Object(o) => Some(o),
            _ => None,
        }
    }

    /// 若当前值是数组，则返回数组切片。
    pub fn as_array(&self) -> Option<&[JsonValue]> {
        match self {
            JsonValue::Array(v) => Some(v),
            _ => None,
        }
    }

    /// 若当前值是字符串，则返回字符串切片。
    pub fn as_str(&self) -> Option<&str> {
        match self {
            JsonValue::String(s) => Some(s),
            _ => None,
        }
    }

    /// 若当前值是布尔值，则返回该布尔值。
    pub fn as_bool(&self) -> Option<bool> {
        match self {
            JsonValue::Bool(b) => Some(*b),
            _ => None,
        }
    }

    /// 若当前值是可表示为 `i64` 的数字，则返回该整数。
    pub fn as_i64(&self) -> Option<i64> {
        match self {
            JsonValue::Number(n) => n.as_i64(),
            _ => None,
        }
    }

    /// 若当前值是数字，则返回其 `f64` 表示。
    pub fn as_f64(&self) -> Option<f64> {
        match self {
            JsonValue::Number(n) => n.as_f64(),
            _ => None,
        }
    }

    fn write_to(&self, out: &mut String) {
        match self {
            JsonValue::Null => out.push_str("null"),
            JsonValue::Bool(b) => out.push_str(if *b { "true" } else { "false" }),
            JsonValue::Number(JsonNumber::I64(v)) => out.push_str(&v.to_string()),
            JsonValue::Number(JsonNumber::F64(v)) => {
                // 非有限浮点数没有合法 JSON 表示，这里保持现有行为并回落为 `null`。
                if v.is_finite() {
                    out.push_str(&format!("{}", v));
                } else {
                    out.push_str("null");
                }
            }
            JsonValue::String(s) => write_json_string(out, s),
            JsonValue::Array(items) => {
                out.push('[');
                for (i, item) in items.iter().enumerate() {
                    if i != 0 {
                        out.push(',');
                    }
                    item.write_to(out);
                }
                out.push(']');
            }
            JsonValue::Object(map) => {
                out.push('{');
                for (i, (k, v)) in map.iter().enumerate() {
                    if i != 0 {
                        out.push(',');
                    }
                    write_json_string(out, k);
                    out.push(':');
                    v.write_to(out);
                }
                out.push('}');
            }
        }
    }
}

fn write_json_string(out: &mut String, s: &str) {
    out.push('"');
    for ch in s.chars() {
        match ch {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\u{08}' => out.push_str("\\b"),
            '\u{0C}' => out.push_str("\\f"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            c if (c as u32) < 0x20 => out.push_str(&format!("\\u{:04x}", c as u32)),
            c => out.push(c),
        }
    }
    out.push('"');
}

struct Parser<'a> {
    input: &'a [u8],
    idx: usize,
}

impl<'a> Parser<'a> {
    fn new(input: &'a [u8]) -> Self {
        Self { input, idx: 0 }
    }

    fn eof(&self) -> bool {
        self.idx >= self.input.len()
    }

    fn peek(&self) -> Option<u8> {
        self.input.get(self.idx).copied()
    }

    fn next(&mut self) -> Option<u8> {
        let b = self.peek()?;
        self.idx += 1;
        Some(b)
    }

    fn skip_ws(&mut self) {
        while let Some(b) = self.peek() {
            match b {
                b' ' | b'\n' | b'\r' | b'\t' => self.idx += 1,
                _ => break,
            }
        }
    }

    fn parse_value(&mut self) -> Result<JsonValue> {
        self.skip_ws();
        let b = self
            .peek()
            .ok_or_else(|| SdkError::JsonParse("Unexpected EOF".to_string()))?;
        match b {
            b'n' => self.parse_null(),
            b't' => self.parse_true(),
            b'f' => self.parse_false(),
            b'"' => Ok(JsonValue::String(self.parse_string()?)),
            b'[' => self.parse_array(),
            b'{' => self.parse_object(),
            b'-' | b'0'..=b'9' => self.parse_number(),
            _ => Err(SdkError::JsonParse(format!(
                "Unexpected byte '{}' at {}",
                b as char, self.idx
            ))),
        }
    }

    fn expect_bytes(&mut self, expected: &[u8]) -> Result<()> {
        for &b in expected {
            let got = self.next().ok_or_else(|| {
                SdkError::JsonParse(format!(
                    "Unexpected EOF while expecting '{}'",
                    String::from_utf8_lossy(expected)
                ))
            })?;
            if got != b {
                return Err(SdkError::JsonParse(format!(
                    "Expected '{}' but got '{}' at byte {}",
                    b as char, got as char, self.idx - 1
                )));
            }
        }
        Ok(())
    }

    fn parse_null(&mut self) -> Result<JsonValue> {
        self.expect_bytes(b"null")?;
        Ok(JsonValue::Null)
    }

    fn parse_true(&mut self) -> Result<JsonValue> {
        self.expect_bytes(b"true")?;
        Ok(JsonValue::Bool(true))
    }

    fn parse_false(&mut self) -> Result<JsonValue> {
        self.expect_bytes(b"false")?;
        Ok(JsonValue::Bool(false))
    }

    fn parse_string(&mut self) -> Result<String> {
        let quote = self.next();
        if quote != Some(b'"') {
            return Err(SdkError::JsonParse("Expected '\"'".to_string()));
        }

        // 按 UTF-8 字节累积内容，支持 `\uXXXX` 和代理对，控制字符必须转义。
        let mut out: Vec<u8> = Vec::new();
        while let Some(b) = self.next() {
            match b {
                b'"' => {
                    return String::from_utf8(out)
                        .map_err(|e| SdkError::JsonParse(e.to_string()))
                }
                b'\\' => {
                    let esc = self.next().ok_or_else(|| {
                        SdkError::JsonParse("Unexpected EOF in escape".to_string())
                    })?;
                    match esc {
                        b'"' => out.push(b'"'),
                        b'\\' => out.push(b'\\'),
                        b'/' => out.push(b'/'),
                        b'b' => out.push(0x08),
                        b'f' => out.push(0x0C),
                        b'n' => out.push(b'\n'),
                        b'r' => out.push(b'\r'),
                        b't' => out.push(b'\t'),
                        b'u' => {
                            let cp = self.parse_hex_u16()? as u32;
                            if (0xD800..=0xDBFF).contains(&cp) {
                                self.expect_bytes(b"\\u")?;
                                let low = self.parse_hex_u16()? as u32;
                                if !(0xDC00..=0xDFFF).contains(&low) {
                                    return Err(SdkError::JsonParse(
                                        "Invalid surrogate pair".to_string(),
                                    ));
                                }
                                let full = 0x10000 + (((cp - 0xD800) << 10) | (low - 0xDC00));
                                push_utf8_char(&mut out, full)?;
                            } else {
                                push_utf8_char(&mut out, cp)?;
                            }
                        }
                        _ => {
                            return Err(SdkError::JsonParse(format!(
                                "Invalid escape '\\{}'",
                                esc as char
                            )))
                        }
                    }
                }
                b if b < 0x20 => {
                    return Err(SdkError::JsonParse(
                        "Unescaped control character in string".to_string(),
                    ))
                }
                b => out.push(b),
            }
        }
        Err(SdkError::JsonParse("Unexpected EOF in string".to_string()))
    }

    fn parse_hex_u16(&mut self) -> Result<u16> {
        let mut v: u16 = 0;
        for _ in 0..4 {
            let b = self.next().ok_or_else(|| {
                SdkError::JsonParse("Unexpected EOF in unicode escape".to_string())
            })?;
            v = (v << 4)
                | match b {
                    b'0'..=b'9' => (b - b'0') as u16,
                    b'a'..=b'f' => (b - b'a' + 10) as u16,
                    b'A'..=b'F' => (b - b'A' + 10) as u16,
                    _ => {
                        return Err(SdkError::JsonParse(format!(
                            "Invalid hex digit '{}' at {}",
                            b as char,
                            self.idx - 1
                        )))
                    }
                };
        }
        Ok(v)
    }

    fn parse_array(&mut self) -> Result<JsonValue> {
        let open = self.next();
        if open != Some(b'[') {
            return Err(SdkError::JsonParse("Expected '['".to_string()));
        }
        self.skip_ws();
        let mut items = Vec::new();
        if self.peek() == Some(b']') {
            self.idx += 1;
            return Ok(JsonValue::Array(items));
        }
        loop {
            let v = self.parse_value()?;
            items.push(v);
            self.skip_ws();
            match self.next() {
                Some(b',') => {
                    self.skip_ws();
                    continue;
                }
                Some(b']') => break,
                Some(other) => {
                    return Err(SdkError::JsonParse(format!(
                        "Expected ',' or ']' but got '{}' at {}",
                        other as char,
                        self.idx - 1
                    )))
                }
                None => return Err(SdkError::JsonParse("Unexpected EOF in array".to_string())),
            }
        }
        Ok(JsonValue::Array(items))
    }

    fn parse_object(&mut self) -> Result<JsonValue> {
        let open = self.next();
        if open != Some(b'{') {
            return Err(SdkError::JsonParse("Expected '{'".to_string()));
        }
        self.skip_ws();
        let mut map: BTreeMap<String, JsonValue> = BTreeMap::new();
        if self.peek() == Some(b'}') {
            self.idx += 1;
            return Ok(JsonValue::Object(map));
        }
        loop {
            self.skip_ws();
            let key = self.parse_string()?;
            self.skip_ws();
            if self.next() != Some(b':') {
                return Err(SdkError::JsonParse("Expected ':'".to_string()));
            }
            self.skip_ws();
            let value = self.parse_value()?;
            map.insert(key, value);
            self.skip_ws();
            match self.next() {
                Some(b',') => {
                    self.skip_ws();
                    continue;
                }
                Some(b'}') => break,
                Some(other) => {
                    return Err(SdkError::JsonParse(format!(
                        "Expected ',' or '}}' but got '{}' at {}",
                        other as char,
                        self.idx - 1
                    )))
                }
                None => return Err(SdkError::JsonParse("Unexpected EOF in object".to_string())),
            }
        }
        Ok(JsonValue::Object(map))
    }

    fn parse_number(&mut self) -> Result<JsonValue> {
        let start = self.idx;
        if self.peek() == Some(b'-') {
            self.idx += 1;
        }
        // 仅接受 JSON 标准数字格式；前导零、空小数和空指数都视为错误。
        match self.peek() {
            Some(b'0') => self.idx += 1,
            Some(b'1'..=b'9') => {
                self.idx += 1;
                while matches!(self.peek(), Some(b'0'..=b'9')) {
                    self.idx += 1;
                }
            }
            _ => {
                return Err(SdkError::JsonParse(format!(
                    "Invalid number at byte {}",
                    self.idx
                )))
            }
        }

        let mut is_float = false;
        if self.peek() == Some(b'.') {
            is_float = true;
            self.idx += 1;
            let mut digits = 0;
            while matches!(self.peek(), Some(b'0'..=b'9')) {
                self.idx += 1;
                digits += 1;
            }
            if digits == 0 {
                return Err(SdkError::JsonParse("Invalid number fraction".to_string()));
            }
        }

        if matches!(self.peek(), Some(b'e') | Some(b'E')) {
            is_float = true;
            self.idx += 1;
            if matches!(self.peek(), Some(b'+') | Some(b'-')) {
                self.idx += 1;
            }
            let mut digits = 0;
            while matches!(self.peek(), Some(b'0'..=b'9')) {
                self.idx += 1;
                digits += 1;
            }
            if digits == 0 {
                return Err(SdkError::JsonParse("Invalid number exponent".to_string()));
            }
        }

        let s = std::str::from_utf8(&self.input[start..self.idx])
            .map_err(|e| SdkError::JsonParse(e.to_string()))?;

        let num = if is_float {
            let f = s
                .parse::<f64>()
                .map_err(|e| SdkError::JsonParse(e.to_string()))?;
            JsonNumber::F64(f)
        } else {
            let i = s
                .parse::<i64>()
                .map_err(|e| SdkError::JsonParse(e.to_string()))?;
            JsonNumber::I64(i)
        };
        Ok(JsonValue::Number(num))
    }
}

fn push_utf8_char(out: &mut Vec<u8>, code_point: u32) -> Result<()> {
    match std::char::from_u32(code_point) {
        Some(ch) => {
            let mut buf = [0u8; 4];
            let s = ch.encode_utf8(&mut buf);
            out.extend_from_slice(s.as_bytes());
            Ok(())
        }
        // 非法码点统一按 JSON 解析错误处理。
        None => Err(SdkError::JsonParse(
            "Invalid unicode codepoint".to_string(),
        )),
    }
}
