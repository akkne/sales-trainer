{{- define "sellevate-service.name" -}}
{{- default .Release.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "sellevate-service.labels" -}}
app.kubernetes.io/name: {{ include "sellevate-service.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end -}}

{{- define "sellevate-service.selectorLabels" -}}
app.kubernetes.io/name: {{ include "sellevate-service.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end -}}
