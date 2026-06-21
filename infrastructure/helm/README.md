# Sellevate Helm charts

A single generic chart, `sellevate-service`, deploys any one Sellevate microservice or the
gateway. Each service is one Helm release, parameterized by a `values/<service>.yaml` file.
This keeps the per-service Kubernetes shape identical (Deployment + Service + liveness/
readiness probes + config/secret refs) while letting each release set its own image,
service type, and environment.

## What the chart renders
- **Deployment** — one container, configurable replicas/resources, env from inline values
  plus an optional ConfigMap (`envFromConfigMap`) and Secret (`envFromSecret`).
- **Service** — `ClusterIP` for internal services, `LoadBalancer` for the gateway.
- **Probes** — `livenessProbe` → `/healthz`, `readinessProbe` → `/readyz` (the shared
  endpoints from Phase 10.1). Readiness gates traffic until Postgres/Redis/Kafka/Mongo are
  reachable; liveness only checks the process is up, so a flaky dependency never restarts a
  healthy pod.

## Prerequisites (managed outside this chart)
The infra dependencies (Postgres, MongoDB, Redis, Kafka, Loki/Prometheus/Grafana) are not
templated here — deploy them via their own operators/charts or as managed services, then
reference them through each service's ConfigMap/Secret. Create those per service before
install, e.g.:

```sh
kubectl create configmap gamification-config \
  --from-literal=ConnectionStrings__Redis=redis:6379
kubectl create secret generic gamification-secrets \
  --from-literal=ConnectionStrings__Postgres='Host=postgres;Database=gamification;...' \
  --from-literal=Jwt__Key='...'
```

## Install / upgrade
```sh
# Fully-worked template service:
helm upgrade --install gamification ./sellevate-service -f values/gamification.yaml

# Gateway (the public entrypoint, LoadBalancer):
helm upgrade --install gateway ./sellevate-service -f values/gateway.yaml
```

Values files exist for all eight releases: `gateway`, `identity`, `learning`,
`gamification`, `ai`, `social`, `analytics`, `notification`.

## Validate without a cluster
```sh
helm lint ./sellevate-service -f values/gamification.yaml
helm template gamification ./sellevate-service -f values/gamification.yaml
```

## Status (Phase 10.5)
`gamification` and `gateway` are the fully-worked references (LoadBalancer + probes +
config/secret wiring). The other six share the same chart with equivalent values; review
their resource limits and dependency wiring before production use.
