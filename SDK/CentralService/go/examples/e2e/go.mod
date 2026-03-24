module ensoai.local/centralservice-e2e

go 1.20

require (
	ensoai.local/centralservice-client v0.0.0
	ensoai.local/centralservice-service v0.0.0
)

replace ensoai.local/centralservice-service => ../../service
replace ensoai.local/centralservice-client => ../../client

