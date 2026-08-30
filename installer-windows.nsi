Unicode True
!include "MUI2.nsh"

!define APP_NAME "Compress my Web"
!define APP_EXE "CompressMyWeb.exe"
!define APP_VERSION "1.3.0"
!define COMPANY_NAME "Alisson Azevedo"

Name "${APP_NAME}"
OutFile "dist\CompressMyWeb-Setup-${APP_VERSION}-win-x64.exe"
InstallDir "$PROGRAMFILES64\CompressMyWeb"
InstallDirRegKey HKLM "Software\CompressMyWeb" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Icon "Assets\favcon-CmW.ico"
UninstallIcon "Assets\favcon-CmW.ico"
VIProductVersion "1.3.0.0"
VIAddVersionKey "ProductName" "${APP_NAME}"
VIAddVersionKey "CompanyName" "${COMPANY_NAME}"
VIAddVersionKey "LegalCopyright" "Copyright © 2026 Alisson Azevedo"
VIAddVersionKey "FileDescription" "Instalador do Compress my Web"
VIAddVersionKey "FileVersion" "${APP_VERSION}"
VIAddVersionKey "ProductVersion" "${APP_VERSION}"

!define MUI_ABORTWARNING
!define MUI_ICON "Assets\favcon-CmW.ico"
!define MUI_UNICON "Assets\favcon-CmW.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "PortugueseBR"

Section "Aplicativo" SecMain
    SetOutPath "$INSTDIR"
    File /r "dist\windows-x64\*.*"

    WriteUninstaller "$INSTDIR\Desinstalar.exe"
    WriteRegStr HKLM "Software\CompressMyWeb" "InstallDir" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb" "DisplayName" "${APP_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb" "DisplayVersion" "${APP_VERSION}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb" "Publisher" "${COMPANY_NAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb" "DisplayIcon" "$INSTDIR\${APP_EXE}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb" "UninstallString" '"$INSTDIR\Desinstalar.exe"'

    CreateDirectory "$SMPROGRAMS\CompressMyWeb"
    CreateShortcut "$SMPROGRAMS\CompressMyWeb\Compress my Web.lnk" "$INSTDIR\${APP_EXE}"
    CreateShortcut "$SMPROGRAMS\CompressMyWeb\Desinstalar.lnk" "$INSTDIR\Desinstalar.exe"
    CreateShortcut "$DESKTOP\Compress my Web.lnk" "$INSTDIR\${APP_EXE}"

    SearchPath $0 "qpdf.exe"
    SearchPath $1 "gswin64c.exe"
    StrCmp $0 "" DependenciesMissing
    StrCmp $1 "" DependenciesMissing DependenciesReady

DependenciesMissing:
    MessageBox MB_OK|MB_ICONINFORMATION "O aplicativo foi instalado. Para comprimir PDFs, instale também qpdf e Ghostscript e adicione-os ao PATH do Windows. A conversão de imagens funciona normalmente sem esses componentes."
DependenciesReady:
SectionEnd

Section "Uninstall"
    Delete "$DESKTOP\Compress my Web.lnk"
    Delete "$SMPROGRAMS\CompressMyWeb\Compress my Web.lnk"
    Delete "$SMPROGRAMS\CompressMyWeb\Desinstalar.lnk"
    RMDir "$SMPROGRAMS\CompressMyWeb"
    RMDir /r "$INSTDIR"
    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\CompressMyWeb"
    DeleteRegKey HKLM "Software\CompressMyWeb"
SectionEnd
