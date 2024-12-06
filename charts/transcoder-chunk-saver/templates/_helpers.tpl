{{/*
Expand the name of the chart.
*/}}
{{- define "transcoder-chunk-saver.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "transcoder-chunk-saver.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "transcoder-chunk-saver.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "transcoder-chunk-saver.labels" -}}
helm.sh/chart: {{ include "transcoder-chunk-saver.chart" . }}
{{ include "transcoder-chunk-saver.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "transcoder-chunk-saver.selectorLabels" -}}
app.kubernetes.io/name: {{ include "transcoder-chunk-saver.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "transcoder-chunk-saver.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "transcoder-chunk-saver.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Pull up connectionstring from existing secret
*/}}
{{- define "transcoder-chunk-saver.dbConnectionString" -}}
{{- $secret := (lookup "v1" "Secret" .Release.Namespace .Values.dbConnectionString.secretNameRef) }}
{{- if and $secret (hasKey $secret.data "connectionString") }}
{{- $secret.data.connectionString }}
{{- else }}
{{- default "<connection_string>" | b64enc }}
{{- end }}
{{- end }}

