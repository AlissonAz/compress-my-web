Unicode True
!include "MUI2.nsh"
!include "LogicLib.nsh"

!define APP_NAME "Compress my Web"
!define APP_EXE "CompressMyWeb.exe"
!define APP_VERSION "1.6.0"
!define COMPANY_NAME "Alisson Azevedo"

Name "${APP_NAME}"
OutFile "dist\CompressMyWeb-Setup-${APP_VERSION}-win-x64.exe"
InstallDir "$PROGRAMFILES64\CompressMyWeb"
InstallDirRegKey HKLM "Software\CompressMyWeb" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
Icon "Assets\favcon-CmW.ico"
UninstallIcon "Assets\favcon-CmW.ico"
VIProductVersion "1.6.0.0"
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

    SetOutPath "$INSTDIR\tools\qpdf"
    File /r "dist\windows-dependencies\qpdf\*.*"

    SetOutPath "$INSTDIR\tools\ghostscript"
    File /r "dist\windows-dependencies\ghostscript\*.*"

    SetOutPath "$INSTDIR\sources"
    File "dist\windows-dependencies\sources\ghostscript-10.07.1.tar.xz"
    File "dist\windows-dependencies\sources\compress-my-web-1.6.0.tar.gz"

    SetOutPath "$INSTDIR"

    ; Verificar se o Microsoft Visual C++ 2015-2022 (x64) já está instalado no Windows
    SetRegView 64
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\X64" "Installed"
    SetRegView 32

    ${If} $0 != 1
        DetailPrint "Instalando componente Microsoft Visual C++ (x64)..."
        ${If} ${FileExists} "$INSTDIR\tools\ghostscript\vcredist_x64.exe"
            ExecWait '"$INSTDIR\tools\ghostscript\vcredist_x64.exe" /install /passive /norestart' $1
            ${If} $1 != 0
            ${AndIf} $1 != 1638
            ${AndIf} $1 != 3010
                DetailPrint "Aviso: Microsoft Visual C++ finalizou com código $1."
            ${EndIf}
        ${EndIf}
    ${Else}
        DetailPrint "Microsoft Visual C++ já detectado no sistema."
    ${EndIf}

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
