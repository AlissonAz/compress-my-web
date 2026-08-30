#!/usr/bin/env bash
set -e

APP_VERSION="1.3.0"
PUBLISH_DIR="dist/windows-x64"
INSTALLER="dist/CompressMyWeb-Setup-${APP_VERSION}-win-x64.exe"
NSIS_EXECUTABLE="${NSIS_BIN:-makensis}"

echo "=== 1. Publicando para Windows x64 ==="
dotnet publish CompressMyWeb.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o "${PUBLISH_DIR}"

echo "=== 2. Gerando instalador Windows ==="
if ! command -v "${NSIS_EXECUTABLE}" >/dev/null 2>&1 && [[ ! -x "${NSIS_EXECUTABLE}" ]]; then
  echo "Erro: makensis não encontrado. Instale o pacote 'nsis' ou informe NSIS_BIN."
  exit 1
fi

"${NSIS_EXECUTABLE}" installer-windows.nsi

echo "=========================================================="
echo " Instalador gerado em: ${INSTALLER}"
echo "=========================================================="
