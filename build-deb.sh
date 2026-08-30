#!/usr/bin/env bash
set -e

APP_NAME="compressmyweb"
DISPLAY_NAME="CompressMyWeb"
VERSION="1.4.1"
ARCH="amd64"
OUTPUT_DIR="dist"
BUILD_ROOT="${OUTPUT_DIR}/deb-build"
DEB_PACKAGE="${OUTPUT_DIR}/${APP_NAME}_${VERSION}_${ARCH}.deb"

echo "=== 1. Publicando binários .NET 8 para Linux x64 ==="
dotnet publish CompressMyWeb.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -o "${BUILD_ROOT}/usr/lib/${APP_NAME}"

echo "=== 2. Preparando estrutura do pacote Debian ==="
mkdir -p "${BUILD_ROOT}/DEBIAN"
mkdir -p "${BUILD_ROOT}/usr/bin"
mkdir -p "${BUILD_ROOT}/usr/share/applications"
mkdir -p "${BUILD_ROOT}/usr/share/icons/hicolor/256x256/apps"
mkdir -p "${BUILD_ROOT}/usr/share/pixmaps"
mkdir -p "${BUILD_ROOT}/usr/share/doc/${APP_NAME}"

# Link simbólico para /usr/bin
ln -sf "/usr/lib/${APP_NAME}/CompressMyWeb" "${BUILD_ROOT}/usr/bin/${APP_NAME}"

# Arquivo DEBIAN/control
cat << CONTROL_EOF > "${BUILD_ROOT}/DEBIAN/control"
Package: ${APP_NAME}
Version: ${VERSION}
Section: graphics
Priority: optional
Architecture: ${ARCH}
Maintainer: CompressMyWeb Team <contato@compressmyweb.local>
Depends: libc6, libgcc-s1, qpdf, ghostscript
Description: Compressor e conversor de arquivos em lote
 CompressMyWeb é uma aplicação desktop desenvolvida em C# para comprimir e
 converter arquivos sequencial em lote.
CONTROL_EOF

# Arquivo .desktop para o menu do Linux Mint
cat << DESKTOP_EOF > "${BUILD_ROOT}/usr/share/applications/${APP_NAME}.desktop"
[Desktop Entry]
Name=${DISPLAY_NAME}
Comment=Conversor de imagens e compressor estrutural de PDFs
Exec=/usr/bin/${APP_NAME}
Icon=${APP_NAME}
Terminal=false
Type=Application
Categories=Graphics;Photography;Utility;
Keywords=webp;image;pdf;compress;optimizer;converter;
StartupNotify=true
DESKTOP_EOF

# Copiar ícone PNG oficial de alta resolução
cp "Assets/favcon CmW.png" "${BUILD_ROOT}/usr/share/icons/hicolor/256x256/apps/${APP_NAME}.png"
cp "Assets/favcon CmW.png" "${BUILD_ROOT}/usr/share/pixmaps/${APP_NAME}.png"
cp LICENSE "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/LICENSE"
cp THIRD-PARTY-NOTICES.md "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/THIRD-PARTY-NOTICES.md"
cp SECURITY.md "${BUILD_ROOT}/usr/share/doc/${APP_NAME}/SECURITY.md"

echo "=== 3. Ajustando permissões ==="
chmod -R 755 "${BUILD_ROOT}/usr"
chmod 755 "${BUILD_ROOT}/DEBIAN"
chmod 755 "${BUILD_ROOT}/usr/lib/${APP_NAME}/CompressMyWeb"

echo "=== 4. Gerando pacote .deb ==="
dpkg-deb --build "${BUILD_ROOT}" "${DEB_PACKAGE}"

echo "=========================================================="
echo " Pacote gerado com sucesso em: ${DEB_PACKAGE}"
echo " Para instalar no Linux Mint execute:"
echo "   sudo dpkg -i ${DEB_PACKAGE}"
echo "=========================================================="
