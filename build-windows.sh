#!/usr/bin/env bash
set -e

APP_VERSION="1.6.3"
QPDF_VERSION="12.3.2"
GHOSTSCRIPT_VERSION="10.07.1"
PUBLISH_DIR="dist/windows-x64"
DEPENDENCIES_DIR="dist/windows-dependencies"
CACHE_DIR=".cache/windows-dependencies"
INSTALLER="dist/CompressMyWeb-Setup-${APP_VERSION}-win-x64.exe"
NSIS_EXECUTABLE="${NSIS_BIN:-makensis}"

QPDF_ARCHIVE="${CACHE_DIR}/qpdf-${QPDF_VERSION}-msvc64.zip"
GHOSTSCRIPT_INSTALLER="${CACHE_DIR}/gs10071w64.exe"
GHOSTSCRIPT_SOURCE="${CACHE_DIR}/ghostscript-${GHOSTSCRIPT_VERSION}.tar.xz"

download_and_verify() {
  local url="$1"
  local destination="$2"
  local expected_hash="$3"

  if [[ ! -f "${destination}" ]]; then
    curl -fL --retry 3 --create-dirs -o "${destination}" "${url}"
  fi

  echo "${expected_hash}  ${destination}" | sha256sum --check --status || {
    echo "Erro: SHA-256 inválido para ${destination}."
    exit 1
  }
}

command -v curl >/dev/null 2>&1 || { echo "Erro: curl não encontrado."; exit 1; }
command -v 7z >/dev/null 2>&1 || { echo "Erro: 7z não encontrado."; exit 1; }

echo "=== 1. Preparando dependências offline verificadas ==="
download_and_verify \
  "https://github.com/qpdf/qpdf/releases/download/v${QPDF_VERSION}/qpdf-${QPDF_VERSION}-msvc64.zip" \
  "${QPDF_ARCHIVE}" \
  "8941870a604e7c87ed24566b038d46c24ce76616254d2383c578f60c0677f202"
download_and_verify \
  "https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10071/gs10071w64.exe" \
  "${GHOSTSCRIPT_INSTALLER}" \
  "3a4c28d0aac47aa7cccd35a5932c55110376e9dbd966898dde388b7faba444a4"
download_and_verify \
  "https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10071/ghostscript-${GHOSTSCRIPT_VERSION}.tar.xz" \
  "${GHOSTSCRIPT_SOURCE}" \
  "1cdb766de8db8f1e589c817f09c5855ea5f65dfc8540e465a69ac14c18416025"

rm -rf "${DEPENDENCIES_DIR}"
mkdir -p "${DEPENDENCIES_DIR}/qpdf" "${DEPENDENCIES_DIR}/ghostscript" "${DEPENDENCIES_DIR}/sources"
7z x -y "${QPDF_ARCHIVE}" "-o${DEPENDENCIES_DIR}/qpdf-extracted" >/dev/null
cp -a "${DEPENDENCIES_DIR}/qpdf-extracted/qpdf-${QPDF_VERSION}-msvc64/." "${DEPENDENCIES_DIR}/qpdf/"
7z x -y "${GHOSTSCRIPT_INSTALLER}" "-o${DEPENDENCIES_DIR}/ghostscript-extracted" >/dev/null
cp -a "${DEPENDENCIES_DIR}/ghostscript-extracted/bin" "${DEPENDENCIES_DIR}/ghostscript/"
cp -a "${DEPENDENCIES_DIR}/ghostscript-extracted/lib" "${DEPENDENCIES_DIR}/ghostscript/"
cp -a "${DEPENDENCIES_DIR}/ghostscript-extracted/Resource" "${DEPENDENCIES_DIR}/ghostscript/"
cp -a "${DEPENDENCIES_DIR}/ghostscript-extracted/iccprofiles" "${DEPENDENCIES_DIR}/ghostscript/"
cp -a "${DEPENDENCIES_DIR}/ghostscript-extracted/doc" "${DEPENDENCIES_DIR}/ghostscript/"
cp "${DEPENDENCIES_DIR}/ghostscript-extracted/vcredist_x64.exe" "${DEPENDENCIES_DIR}/ghostscript/"
cp "${GHOSTSCRIPT_SOURCE}" "${DEPENDENCIES_DIR}/sources/"
tar \
  --exclude='./.git' --exclude='./.cache' --exclude='./bin' --exclude='./obj' --exclude='./dist' \
  -czf "${DEPENDENCIES_DIR}/sources/compress-my-web-${APP_VERSION}.tar.gz" .
rm -rf "${DEPENDENCIES_DIR}/qpdf-extracted" "${DEPENDENCIES_DIR}/ghostscript-extracted"

echo "=== 2. Publicando para Windows x64 ==="
dotnet publish CompressMyWeb.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=false \
  -o "${PUBLISH_DIR}"

echo "=== 3. Gerando instalador Windows offline ==="
if ! command -v "${NSIS_EXECUTABLE}" >/dev/null 2>&1 && [[ ! -x "${NSIS_EXECUTABLE}" ]]; then
  echo "Aviso: makensis não encontrado. O instalador NSIS não foi gerado. Instale o pacote 'nsis' ou informe NSIS_BIN."
else
  "${NSIS_EXECUTABLE}" installer-windows.nsi
  echo " Instalador gerado em: ${INSTALLER}"
fi

echo "=== 4. Gerando pacote portátil (.zip) para Windows ==="
PORTABLE_ROOT="dist/portable"
PORTABLE_APP_DIR="${PORTABLE_ROOT}/CompressMyWeb"
PORTABLE_ZIP="dist/CompressMyWeb-v${APP_VERSION}-win-x64-portable.zip"

rm -rf "${PORTABLE_ROOT}" "${PORTABLE_ZIP}"
mkdir -p "${PORTABLE_APP_DIR}"
cp -a "${PUBLISH_DIR}/." "${PORTABLE_APP_DIR}/"
mkdir -p "${PORTABLE_APP_DIR}/tools/qpdf" "${PORTABLE_APP_DIR}/tools/ghostscript"
cp -a "${DEPENDENCIES_DIR}/qpdf/." "${PORTABLE_APP_DIR}/tools/qpdf/"
cp -a "${DEPENDENCIES_DIR}/ghostscript/." "${PORTABLE_APP_DIR}/tools/ghostscript/"
cp LICENSE "${PORTABLE_APP_DIR}/"
cp README.md "${PORTABLE_APP_DIR}/"
cp THIRD-PARTY-NOTICES.md "${PORTABLE_APP_DIR}/"
cp SECURITY.md "${PORTABLE_APP_DIR}/"

(cd "${PORTABLE_ROOT}" && 7z a -tzip -mx=9 "../CompressMyWeb-v${APP_VERSION}-win-x64-portable.zip" "CompressMyWeb") >/dev/null
rm -rf "${PORTABLE_ROOT}"

echo " Pacote portátil gerado em: ${PORTABLE_ZIP}"
echo "=========================================================="
echo " Build para Windows concluído com sucesso!"
echo "=========================================================="
