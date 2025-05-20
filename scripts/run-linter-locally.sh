#!/bin/bash
docker run --rm \
	-e LOG_LEVEL=INFO \
	-e RUN_LOCAL=true \
	-e DEFAULT_BRANCH="$(git branch --show-current)" \
	-e VALIDATE_DOTNET_SLN_FORMAT_ANALYZERS=true \
	-e FIX_DOTNET_SLN_FORMAT_ANALYZERS=true \
	-e VALIDATE_DOTNET_SLN_FORMAT_STYLE=true \
	-e FIX_DOTNET_SLN_FORMAT_STYLE=true \
	-e VALIDATE_DOTNET_SLN_FORMAT_WHITESPACE=true \
	-e FIX_DOTNET_SLN_FORMAT_WHITESPACE=true \
	-e VALIDATE_YAML_PRETTIER=true \
	-e FIX_YAML_PRETTIER=true \
	-e VALIDATE_BASH=true \
	-e VALIDATE_BASH_EXEC=true \
	-e VALIDATE_SHELL_SHFMT=true \
	-e FIX_SHELL_SHFMT=true \
	-e VALIDATE_JSCPD=true \
	-e VALIDATE_JSON_PRETTIER=true \
	-e FIX_JSON_PRETTIER=true \
	-e JSCPD_CONFIG_FILE=.jscpd.json \
	-v "$CODESPACE_VSCODE_FOLDER":/tmp/lint \
	-v "$CODESPACE_VSCODE_FOLDER/../super-linter-output":/tmp/super-linter-output \
	ghcr.io/super-linter/super-linter:latest
