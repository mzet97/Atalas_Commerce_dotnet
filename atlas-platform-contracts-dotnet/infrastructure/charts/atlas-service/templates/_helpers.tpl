{{- define "atlas-service.name" -}}
{{ .Release.Name }}-{{ .Chart.Name }}
{{- end -}}
