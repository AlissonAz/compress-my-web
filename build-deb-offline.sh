#!/usr/bin/env bash
set -euo pipefail

APP_NAME="compressmyweb"
DISPLAY_NAME="CompressMyWeb"
VERSION="1.6.0"
ARCH="amd64"
OUTPUT_DIR="dist"
BUILD_ROOT="${OUTPUT_DIR}/deb-offline-build"
DEB_PACKAGE="${OUTPUT_DIR}/${APP_NAME}_${VERSION}_${ARCH}-offline.deb"
TOOLS_ROOT="${BUILD_ROOT}/usr/lib/${APP_NAME}/tools"
CACHE_DIR=".cache/linux-dependencies"
GS_SOURCE="${CACHE_DIR}/ghostscript-10.07.1.tar.xz"

command -v qpdf >/dev/null || { echo "Erro: qpdf precisa estar instalado no ambiente de build."; exit 1; }
command -v gs >/dev/null || { echo "Erro: Ghostscript precisa estar instalado no ambiente de build."; exit 1; }
command -v dpkg-deb >/dev/null || { echo "Erro: dpkg-deb não encontrado."; exit 1; }
command -v tar >/dev/null || { echo "Erro: tar não encontrado."; exit 1; }

rm -rf "${BUILD_ROOT}"
mkdir -p "${BUILD_ROOT}/DEBIAN" "${BUILD_ROOT}/usr/bin" \
  "${BUILD_ROOT}/usr/share/applications" \
  "${BUILD_ROOT}/usr/share/icons/hicolor/256x256/apps" \
  "${BUILD_ROOT}/usr/share/pixmaps" "${BUILD_ROOT}/usr/share/doc/${APP_NAME}" \
  "${TOOLS_ROOT}/qpdf/bin" "${TOOLS_ROOT}/qpdf/lib" \
  "${TOOLS_ROOT}/ghostscript/bin" "${TOOLS_ROOT}/ghostscript/lib" \
  "${TOOLS_ROOT}/ghostscript/Resource" "${TOOLS_ROOT}/ghostscript/fonts" \
  "${BUILD_ROOT}/usr/share/${APP_NAME}/sources"

echo "=== 1. Publicando aplicação Linux x64 ==="
dotnet publish CompressMyWeb.csproj -c Release -r linux-x64 --self-contained true --no-restore -p:PublishSingleFile=false -o "${BUILD_ROOT}/usr/lib/${APP_NAME}"

echo "=== 2. Copiando qpdf e Ghostscript locais ==="
cp "$(command -v qpdf)" "${TOOLS_ROOT}/qpdf/bin/qpdf"
cp "$(command -v gs)" "${TOOLS_ROOT}/ghostscript/bin/gs"

copy_runtime_libraries() {
  local executable="$1"
  local destination="$2"
  ldd "${executable}" | awk '/=> \/|^\// { for (i=1; i<=NF; i++) if ($i ~ /^\//) { print $i; break } }' | \
    while IFS= read -r library; do
      [[ -f "${library}" ]] || continue
      case "$(basename "${library}")" in
        libc.so.*|libm.so.*|libpthread.so.*|libdl.so.*|librt.so.*|libgcc_s.so.*|libstdc++.so.*|ld-linux*.so.*) continue ;;
      esac
      cp -L -n "${library}" "${destination}/"
    done
}
copy_runtime_libraries "${TOOLS_ROOT}/qpdf/bin/qpdf" "${TOOLS_ROOT}/qpdf/lib"
copy_runtime_libraries "${TOOLS_ROOT}/ghostscript/bin/gs" "${TOOLS_ROOT}/ghostscript/lib"

GS_PREFIX="$(dirname "$(dirname "$(readlink -f "$(command -v gs)")")")"
if [[ -d "${GS_PREFIX}/share/ghostscript" ]]; then
  GS_RESOURCE_DIR="$(find "${GS_PREFIX}/share/ghostscript" -mindepth 1 -maxdepth 1 -type d -name '[0-9]*' | sort -V | tail -1)"
  [[ -n "${GS_RESOURCE_DIR}" ]] || { echo "Erro: recursos do Ghostscript não encontrados."; exit 1; }
  cp -a "${GS_RESOURCE_DIR}/Resource/." "${TOOLS_ROOT}/ghostscript/Resource/"
  cp -a "${GS_RESOURCE_DIR}/lib/." "${TOOLS_ROOT}/ghostscript/lib/" 2>/dev/null || true
fi
if [[ -d /usr/share/ghostscript/fonts ]]; then cp -a /usr/share/ghostscript/fonts/. "${TOOLS_ROOT}/ghostscript/fonts/"; fi
if [[ -d /var/lib/ghostscript/fonts ]]; then cp -a /var/lib/ghostscript/fonts/. "${TOOLS_ROOT}/ghostscript/fonts/"; fi

if [[ ! -f "${GS_SOURCE}" ]]; then
  mkdir -p "${CACHE_DIR}"
  curl -fL --retry 3 -o "${GS_SOURCE}" \
    https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10071/ghostscript-10.07.1.tar.xz
fi
echo "1cdb766de8db8f1e589c817f09c5855ea5f65dfc8540e465a69ac14c18416025  ${GS_SOURCE}" | sha256sum --check --status
cp "${GS_SOURCE}" "${BUILD_ROOT}/usr/share/${APP_NAME}/sources/"

ln -sf "/usr/lib/${APP_NAME}/CompressMyWeb" "${BUILD_ROOT}/usr/bin/${APP_NAME}"
cat > "${BUILD_ROOT}/DEBIAN/control" << CONTROL_EOF
Package: ${APP_NAME}
Version: ${VERSION}
Section: graphics
Priority: optional
Architecture: ${ARCH}
Maintainer: CompressMyWeb Team <contato@compressmyweb.local>
Depends: libc6, libgcc-s1, libstdc++6
Description: Compressor e conversor de arquivos em lote (offline)
 CompressMyWeb é uma aplicação desktop desenvolvida em C# para comprimir e
 converter arquivos sequencial em lote, com ferramentas PDF incluídas.
CONTROL_EOF
cat > "${BUILD_ROOT}/usr/share/applications/${APP_NAME}.desktop" << DESKTOP_EOF
[Desktop Entry]
Name=${DISPLAY_NAME}
Comment=Conversor de imagens e compressor estrutural de PDFs
Exec=/usr/bin/${APP_NAME}
Icon=${APP_NAME}
Terminal=false
Type=Application
Categories=Graphics;Photography;Utility;
StartupNotify=true
DESKTOP_EOF
cp "Assets/favcon CmW.png" "${BUILD_ROOT}/usr/share/icons/hicolor/256x256/apps/${APP_NAME}.png"
cp "Assets/favcon CmW.png" "${BUILD_ROOT}/usr/share/pixmaps/${APP_NAME}.png"
cp LICENSE THIRD-PARTY-NOTICES.md SECURITY.md "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/"
cp /usr/share/doc/qpdf/copyright "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/qpdf-copyright" 2>/dev/null || true
cp /usr/share/doc/ghostscript/copyright "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/ghostscript-copyright" 2>/dev/null || true
tar --exclude='./.git' --exclude='./.cache' --exclude='./bin' --exclude='./obj' --exclude='./dist' \
  -czf "${BUILD_ROOT}/usr/share/${APP_NAME}/sources/compress-my-web-${VERSION}.tar.gz" .
find "${BUILD_ROOT}/usr/bin" "${BUILD_ROOT}/usr/lib/${APP_NAME}" -type d -exec chmod 755 {} +
find "${BUILD_ROOT}/usr/bin" "${BUILD_ROOT}/usr/lib/${APP_NAME}" -type f -exec chmod 755 {} +
find "${BUILD_ROOT}/usr/share/${APP_NAME}/sources" "${BUILD_ROOT}/usr/share/doc/${APP_NAME}" -type d -exec chmod 755 {} +
find "${BUILD_ROOT}/usr/share/${APP_NAME}/sources" "${BUILD_ROOT}/usr/share/doc/${APP_NAME}" -type f -exec chmod 644 {} +
chmod 755 "${BUILD_ROOT}/DEBIAN"
dpkg-deb --build "${BUILD_ROOT}" "${DEB_PACKAGE}"
echo "Pacote offline gerado: ${DEB_PACKAGE}"
