{{/*
Expand the name of the chart.
*/}}
{{- define "transcoder-composer.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
We truncate at 63 chars because some Kubernetes name fields are limited to this (by the DNS naming spec).
If release name contains chart name it will be used as a full name.
*/}}
{{- define "transcoder-composer.fullname" -}}
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
{{- define "transcoder-composer.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "transcoder-composer.labels" -}}
helm.sh/chart: {{ include "transcoder-composer.chart" . }}
{{ include "transcoder-composer.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "transcoder-composer.selectorLabels" -}}
app.kubernetes.io/name: {{ include "transcoder-composer.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "transcoder-composer.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "transcoder-composer.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
Pull up connectionstring from existing secret
*/}}
{{- define "transcoder-composer.dbConnectionString" -}}
{{- $secret := (lookup "v1" "Secret" .Release.Namespace .Values.dbConnectionString.secretNameRef) }}
{{- if and $secret (hasKey $secret.data "connectionString") }}
{{- $secret.data.connectionString }}
{{- else }}
{{- default "<connection_string>" | b64enc }}
{{- end }}
{{- end }}

