package centralservice

// ApiResponse 表示中心服务 API 通用响应包裹结构。
type ApiResponse[T any] struct {
	// Success 表示业务请求是否成功。
	Success bool `json:"success"`
	// ErrorCode 表示业务错误码，成功时通常为空。
	ErrorCode *int `json:"errorCode"`
	// ErrorMessage 表示服务端返回的业务错误信息。
	ErrorMessage string `json:"errorMessage"`
	// Data 承载具体业务响应数据。
	Data T `json:"data"`
}

// ServiceRegistrationRequest 描述待注册的服务实例信息。
type ServiceRegistrationRequest struct {
	// Id 是服务实例唯一标识。
	Id string `json:"id"`
	// Name 是服务名称。
	Name string `json:"name"`
	// Host 是服务访问地址。
	Host string `json:"host"`
	// LocalIp 是服务实例所在机器的局域网地址。
	LocalIp string `json:"localIp"`
	// OperatorIp 是发起方所在网络标识，用于服务端入口地址选择。
	OperatorIp string `json:"operatorIp"`
	// PublicIp 是服务实例对发现方暴露的公网或直连地址。
	PublicIp string `json:"publicIp"`
	// Port 是服务监听端口。
	Port int `json:"port"`
	// ServiceType 表示服务类别。
	ServiceType string `json:"serviceType"`
	// HealthCheckUrl 是健康检查地址。
	HealthCheckUrl string `json:"healthCheckUrl"`
	// HealthCheckPort 是健康检查端口。
	HealthCheckPort int `json:"healthCheckPort"`
	// HealthCheckType 表示健康检查方式。
	HealthCheckType string `json:"healthCheckType"`
	// Weight 表示加权发现时使用的权重。
	Weight int `json:"weight"`
	// Metadata 保存附加元数据。
	Metadata map[string]string `json:"metadata"`
}

// ServiceRegistrationResponse 表示服务注册成功后的返回信息。
type ServiceRegistrationResponse struct {
	// Id 是已注册服务实例的唯一标识。
	Id string `json:"id"`
	// RegisterTimestamp 是服务端记录的注册时间戳。
	RegisterTimestamp int64 `json:"registerTimestamp"`
}

// ServiceHeartbeatRequest 表示心跳请求体。
type ServiceHeartbeatRequest struct {
	// Id 是需要续约的服务实例标识。
	Id string `json:"id"`
}

// ServiceListResponse 表示服务列表查询结果。
type ServiceListResponse struct {
	// Services 包含当前查询命中的服务实例。
	Services []ServiceInfo `json:"services"`
}

// ServiceInfo 表示单个服务实例的发现信息。
type ServiceInfo struct {
	// Id 是服务实例唯一标识。
	Id string `json:"id"`
	// Name 是服务名称。
	Name string `json:"name"`
	// Host 是服务地址。
	Host string `json:"host"`
	// Port 是服务端口。
	Port int `json:"port"`
	// Url 是服务完整访问地址。
	Url string `json:"url"`
	// ServiceType 表示服务类别。
	ServiceType string `json:"serviceType"`
	// Status 表示服务端记录的状态码。
	Status int `json:"status"`
	// HealthCheckUrl 是健康检查地址。
	HealthCheckUrl string `json:"healthCheckUrl"`
	// HealthCheckPort 是健康检查端口。
	HealthCheckPort int `json:"healthCheckPort"`
	// HealthCheckType 表示健康检查方式。
	HealthCheckType string `json:"healthCheckType"`
	// RegisterTime 是服务注册时间。
	RegisterTime string `json:"registerTime"`
	// LastHeartbeatTime 是最近一次心跳时间。
	LastHeartbeatTime string `json:"lastHeartbeatTime"`
	// Weight 表示发现时使用的服务权重。
	Weight int `json:"weight"`
	// Metadata 保存附加元数据。
	Metadata map[string]string `json:"metadata"`
	// IsLocalNetwork 表示实例是否被判定为本地网络节点。
	IsLocalNetwork bool `json:"isLocalNetwork"`
}

// ServiceNetworkStatus 表示服务实例的网络探测结果。
type ServiceNetworkStatus struct {
	// ServiceId 是被评估服务实例的标识。
	ServiceId string `json:"serviceId"`
	// ResponseTime 是最近一次评估的响应耗时，单位毫秒。
	ResponseTime int64 `json:"responseTime"`
	// PacketLoss 是最近一次评估的丢包率百分比。
	PacketLoss float64 `json:"packetLoss"`
	// LastCheckTime 是最近一次评估时间。
	LastCheckTime string `json:"lastCheckTime"`
	// ConsecutiveSuccesses 是连续探测成功次数。
	ConsecutiveSuccesses int `json:"consecutiveSuccesses"`
	// ConsecutiveFailures 是连续探测失败次数。
	ConsecutiveFailures int `json:"consecutiveFailures"`
	// IsAvailable 表示该服务当前是否可用。
	IsAvailable bool `json:"isAvailable"`
}

// CalculateScore 根据响应时间和丢包率计算 0 到 100 的网络评分。
func (s ServiceNetworkStatus) CalculateScore() int {
	if !s.IsAvailable {
		return 0
	}

	responseTimeScore := 0
	// 响应时间最多贡献 50 分，50ms 内满分，1000ms 以上记 0 分。
	if s.ResponseTime <= 50 {
		responseTimeScore = 50
	} else if s.ResponseTime >= 1000 {
		responseTimeScore = 0
	} else {
		responseTimeScore = int(50 * (1 - float64(s.ResponseTime-50)/950.0))
	}

	packetLossScore := 0
	// 丢包率最多贡献 50 分，0% 满分，50% 及以上记 0 分。
	if s.PacketLoss <= 0 {
		packetLossScore = 50
	} else if s.PacketLoss >= 50 {
		packetLossScore = 0
	} else {
		packetLossScore = int(50 * (1 - s.PacketLoss/50.0))
	}

	return responseTimeScore + packetLossScore
}

// ProblemDetails 表示 RFC 7807 风格的问题详情。
type ProblemDetails struct {
	// Type 是问题类型标识或文档地址。
	Type string `json:"type"`
	// Title 是问题标题。
	Title string `json:"title"`
	// Status 是问题对应的 HTTP 状态码。
	Status *int `json:"status"`
	// Detail 是问题详情描述。
	Detail string `json:"detail"`
	// Instance 是问题实例标识。
	Instance string `json:"instance"`
	// TraceId 是服务端追踪标识。
	TraceId string `json:"traceId"`
}

// ValidationProblemDetails 表示包含字段级校验错误的问题详情。
type ValidationProblemDetails struct {
	// ProblemDetails 复用通用问题详情字段。
	ProblemDetails
	// Errors 按字段归集校验错误消息。
	Errors map[string][]string `json:"errors"`
}
