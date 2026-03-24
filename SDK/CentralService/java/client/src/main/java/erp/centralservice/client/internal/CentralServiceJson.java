package erp.centralservice.client.internal;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * SDK 内部使用的轻量 JSON 解析与序列化工具。
 *
 * <p>职责：在不引入第三方依赖的前提下完成基础 JSON 解析、序列化以及常见类型转换。
 * 输入约束：{@link #parse(String)} 仅支持标准 JSON 值；{@link #stringify(Object)} 只对
 * {@link Map}、{@link List}、字符串、数字、布尔和 {@code null} 提供结构化输出，其余对象会退化为字符串。
 * 失败语义：语法错误时抛出 {@link IllegalArgumentException}；解析器在读取完首个 JSON 值后仅跳过尾部空白，
 * 不额外校验后续是否还存在非空白字符，这一点属于当前实现的边界条件。</p>
 */
public final class CentralServiceJson {
    private CentralServiceJson() {
    }

    /**
     * 粗略判断一段文本是否可能是 JSON。
     *
     * <p>这是启发式检测，仅检查首个非空白字符是否为 {@code \{} 或 {@code [}，
     * 不能替代真正的 JSON 解析。</p>
     *
     * @param text 待检测文本
     * @return 若首个非空白字符为对象或数组起始符则返回 {@code true}
     */
    public static boolean looksLikeJson(String text) {
        if (text == null || text.isEmpty()) return false;
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            if (Character.isWhitespace(c)) continue;
            return c == '{' || c == '[';
        }
        return false;
    }

    /**
     * 解析 JSON 文本。
     *
     * @param json JSON 文本，可为 {@code null}
     * @return 解析后的对象、数组、字符串、数字、布尔或 {@code null}
     * @throws IllegalArgumentException 当 JSON 语法非法时抛出
     */
    public static Object parse(String json) {
        if (json == null) return null;
        Parser p = new Parser(json);
        Object v = p.parseValue();
        p.skipWs();
        return v;
    }

    /**
     * 将简单 Java 对象树序列化为 JSON 文本。
     *
     * @param value 要序列化的值
     * @return JSON 文本
     */
    public static String stringify(Object value) {
        StringBuilder sb = new StringBuilder();
        writeValue(sb, value);
        return sb.toString();
    }

    /**
     * 将对象安全地视为 JSON 对象节点。
     *
     * @param v 待转换值
     * @return {@link Map} 视图；类型不匹配时返回 {@code null}
     */
    public static Map<String, Object> asObject(Object v) {
        if (v instanceof Map) {
            @SuppressWarnings("unchecked")
            Map<String, Object> m = (Map<String, Object>) v;
            return m;
        }
        return null;
    }

    /**
     * 将对象安全地视为 JSON 数组节点。
     *
     * @param v 待转换值
     * @return {@link List} 视图；类型不匹配时返回 {@code null}
     */
    public static List<Object> asArray(Object v) {
        if (v instanceof List) {
            @SuppressWarnings("unchecked")
            List<Object> a = (List<Object>) v;
            return a;
        }
        return null;
    }

    /**
     * 将任意值转换为字符串。
     *
     * @param v 待转换值
     * @return 字符串结果；输入为 {@code null} 时返回 {@code null}
     */
    public static String asString(Object v) {
        if (v == null) return null;
        if (v instanceof String) return (String) v;
        return String.valueOf(v);
    }

    /**
     * 将任意值转换为可空整数。
     *
     * @param v 待转换值
     * @return 整数结果；转换失败时返回 {@code null}
     */
    public static Integer asIntNullable(Object v) {
        if (v == null) return null;
        if (v instanceof Number) return ((Number) v).intValue();
        try {
            return Integer.parseInt(String.valueOf(v));
        } catch (Exception e) {
            return null;
        }
    }

    /**
     * 将任意值转换为可空长整数。
     *
     * @param v 待转换值
     * @return 长整数结果；转换失败时返回 {@code null}
     */
    public static Long asLongNullable(Object v) {
        if (v == null) return null;
        if (v instanceof Number) return ((Number) v).longValue();
        try {
            return Long.parseLong(String.valueOf(v));
        } catch (Exception e) {
            return null;
        }
    }

    /**
     * 将任意值转换为可空双精度浮点数。
     *
     * @param v 待转换值
     * @return 双精度结果；转换失败时返回 {@code null}
     */
    public static Double asDoubleNullable(Object v) {
        if (v == null) return null;
        if (v instanceof Number) return ((Number) v).doubleValue();
        try {
            return Double.parseDouble(String.valueOf(v));
        } catch (Exception e) {
            return null;
        }
    }

    /**
     * 将任意值转换为布尔值。
     *
     * @param v 待转换值
     * @param def 无法识别时返回的默认值
     * @return 布尔结果
     */
    public static boolean asBoolean(Object v, boolean def) {
        if (v == null) return def;
        if (v instanceof Boolean) return (Boolean) v;
        String s = String.valueOf(v);
        if ("true".equalsIgnoreCase(s)) return true;
        if ("false".equalsIgnoreCase(s)) return false;
        return def;
    }

    /**
     * 将对象节点转换为字符串映射。
     *
     * @param v 待转换值
     * @return 字符串映射；类型不匹配时返回 {@code null}
     */
    public static Map<String, String> asStringMap(Object v) {
        Map<String, Object> obj = asObject(v);
        if (obj == null) return null;
        Map<String, String> out = new LinkedHashMap<String, String>();
        for (Map.Entry<String, Object> e : obj.entrySet()) {
            out.put(e.getKey(), asString(e.getValue()));
        }
        return out;
    }

    /**
     * 递归写入 JSON 值。
     *
     * @param sb 输出缓冲区
     * @param v 当前值
     */
    private static void writeValue(StringBuilder sb, Object v) {
        if (v == null) {
            sb.append("null");
            return;
        }
        if (v instanceof String) {
            writeString(sb, (String) v);
            return;
        }
        if (v instanceof Boolean) {
            sb.append(((Boolean) v) ? "true" : "false");
            return;
        }
        if (v instanceof Number) {
            sb.append(String.valueOf(v));
            return;
        }
        if (v instanceof Map) {
            @SuppressWarnings("unchecked")
            Map<String, Object> m = (Map<String, Object>) v;
            sb.append('{');
            boolean first = true;
            for (Map.Entry<String, Object> e : m.entrySet()) {
                if (!first) sb.append(',');
                first = false;
                writeString(sb, e.getKey());
                sb.append(':');
                writeValue(sb, e.getValue());
            }
            sb.append('}');
            return;
        }
        if (v instanceof List) {
            @SuppressWarnings("unchecked")
            List<Object> a = (List<Object>) v;
            sb.append('[');
            for (int i = 0; i < a.size(); i++) {
                if (i > 0) sb.append(',');
                writeValue(sb, a.get(i));
            }
            sb.append(']');
            return;
        }
        // 对未显式支持的对象类型保持当前兼容行为：退化为字符串字面量输出。
        writeString(sb, String.valueOf(v));
    }

    /**
     * 写入 JSON 字符串字面量。
     *
     * @param sb 输出缓冲区
     * @param s 原始字符串
     */
    private static void writeString(StringBuilder sb, String s) {
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"':
                    sb.append("\\\"");
                    break;
                case '\\':
                    sb.append("\\\\");
                    break;
                case '\b':
                    sb.append("\\b");
                    break;
                case '\f':
                    sb.append("\\f");
                    break;
                case '\n':
                    sb.append("\\n");
                    break;
                case '\r':
                    sb.append("\\r");
                    break;
                case '\t':
                    sb.append("\\t");
                    break;
                default:
                    if (c < 0x20) {
                        String hex = Integer.toHexString(c);
                        sb.append("\\u");
                        for (int j = hex.length(); j < 4; j++) sb.append('0');
                        sb.append(hex);
                    } else {
                        sb.append(c);
                    }
                    break;
            }
        }
        sb.append('"');
    }

    /**
     * 基于游标的最小 JSON 解析器。
     *
     * <p>职责是按顺序消费单个 JSON 值；一旦发现字面量、转义或分隔符不合法，会立即抛出
     * {@link IllegalArgumentException}。</p>
     */
    private static final class Parser {
        /** 原始 JSON 文本。 */
        private final String s;
        /** 当前读取位置。 */
        private int i;

        /**
         * 使用原始 JSON 文本创建解析器。
         *
         * @param s 原始 JSON 文本
         */
        Parser(String s) {
            this.s = s;
            this.i = 0;
        }

        /**
         * 跳过连续空白字符。
         */
        void skipWs() {
            while (i < s.length()) {
                char c = s.charAt(i);
                if (!Character.isWhitespace(c)) return;
                i++;
            }
        }

        /**
         * 解析当前位置的 JSON 值。
         *
         * @return 解析后的值
         */
        Object parseValue() {
            skipWs();
            if (i >= s.length()) return null;
            char c = s.charAt(i);
            if (c == '{') return parseObject();
            if (c == '[') return parseArray();
            if (c == '"') return parseString();
            if (c == 't') return parseLiteral("true", Boolean.TRUE);
            if (c == 'f') return parseLiteral("false", Boolean.FALSE);
            if (c == 'n') return parseLiteral("null", null);
            return parseNumber();
        }

        /**
         * 解析 JSON 字面量。
         *
         * @param lit 期望字面量
         * @param value 匹配成功后返回的值
         * @return 解析结果
         */
        private Object parseLiteral(String lit, Object value) {
            if (s.regionMatches(i, lit, 0, lit.length())) {
                i += lit.length();
                return value;
            }
            throw new IllegalArgumentException("invalid literal at " + i);
        }

        /**
         * 解析 JSON 对象。
         *
         * @return 键值映射
         */
        private Map<String, Object> parseObject() {
            i++; // {
            LinkedHashMap<String, Object> m = new LinkedHashMap<String, Object>();
            skipWs();
            if (i < s.length() && s.charAt(i) == '}') {
                i++;
                return m;
            }
            while (true) {
                skipWs();
                String key = parseString();
                skipWs();
                expect(':');
                Object val = parseValue();
                m.put(key, val);
                skipWs();
                if (tryConsume('}')) break;
                expect(',');
            }
            return m;
        }

        /**
         * 解析 JSON 数组。
         *
         * @return 数组元素列表
         */
        private List<Object> parseArray() {
            i++; // [
            ArrayList<Object> a = new ArrayList<Object>();
            skipWs();
            if (i < s.length() && s.charAt(i) == ']') {
                i++;
                return a;
            }
            while (true) {
                Object val = parseValue();
                a.add(val);
                skipWs();
                if (tryConsume(']')) break;
                expect(',');
            }
            return a;
        }

        /**
         * 解析 JSON 字符串。
         *
         * @return 解码后的字符串
         */
        private String parseString() {
            expect('"');
            StringBuilder sb = new StringBuilder();
            while (i < s.length()) {
                char c = s.charAt(i++);
                if (c == '"') break;
                if (c != '\\') {
                    sb.append(c);
                    continue;
                }
                if (i >= s.length()) throw new IllegalArgumentException("invalid escape at " + i);
                char e = s.charAt(i++);
                switch (e) {
                    case '"':
                        sb.append('"');
                        break;
                    case '\\':
                        sb.append('\\');
                        break;
                    case '/':
                        sb.append('/');
                        break;
                    case 'b':
                        sb.append('\b');
                        break;
                    case 'f':
                        sb.append('\f');
                        break;
                    case 'n':
                        sb.append('\n');
                        break;
                    case 'r':
                        sb.append('\r');
                        break;
                    case 't':
                        sb.append('\t');
                        break;
                    case 'u':
                        sb.append(parseUnicode());
                        break;
                    default:
                        throw new IllegalArgumentException("invalid escape at " + i);
                }
            }
            return sb.toString();
        }

        /**
         * 解析 {@code \\uXXXX} 转义序列。
         *
         * @return 对应的字符
         */
        private char parseUnicode() {
            if (i + 4 > s.length()) throw new IllegalArgumentException("invalid unicode escape at " + i);
            int code = 0;
            for (int j = 0; j < 4; j++) {
                char c = s.charAt(i++);
                int v = Character.digit(c, 16);
                if (v < 0) throw new IllegalArgumentException("invalid unicode escape at " + (i - 1));
                code = (code << 4) | v;
            }
            return (char) code;
        }

        /**
         * 解析 JSON 数字。
         *
         * <p>整数优先尝试转为 {@link Long}；包含小数点或指数时转为 {@link Double}。</p>
         *
         * @return 解析后的数字
         */
        private Number parseNumber() {
            int start = i;
            if (s.charAt(i) == '-') i++;
            while (i < s.length() && isDigit(s.charAt(i))) i++;
            boolean isFloat = false;
            if (i < s.length() && s.charAt(i) == '.') {
                isFloat = true;
                i++;
                while (i < s.length() && isDigit(s.charAt(i))) i++;
            }
            if (i < s.length()) {
                char c = s.charAt(i);
                if (c == 'e' || c == 'E') {
                    isFloat = true;
                    i++;
                    if (i < s.length() && (s.charAt(i) == '+' || s.charAt(i) == '-')) i++;
                    while (i < s.length() && isDigit(s.charAt(i))) i++;
                }
            }
            String num = s.substring(start, i);
            if (!isFloat) {
                try {
                    return Long.parseLong(num);
                } catch (NumberFormatException ignored) {
                }
            }
            return Double.valueOf(num);
        }

        /**
         * 判断字符是否为十进制数字。
         *
         * @param c 待检测字符
         * @return 是否为数字
         */
        private boolean isDigit(char c) {
            return c >= '0' && c <= '9';
        }

        /**
         * 消费一个必须出现的字符。
         *
         * @param c 期望字符
         */
        private void expect(char c) {
            if (i >= s.length() || s.charAt(i) != c) {
                throw new IllegalArgumentException("expected '" + c + "' at " + i);
            }
            i++;
        }

        /**
         * 在当前游标处尝试消费指定字符。
         *
         * @param c 期望字符
         * @return 命中并成功消费时返回 {@code true}
         */
        private boolean tryConsume(char c) {
            if (i < s.length() && s.charAt(i) == c) {
                i++;
                return true;
            }
            return false;
        }
    }
}

