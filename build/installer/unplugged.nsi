; Unplugged: one-click Windows setup (built on Linux with makensis; see build/package.sh)
; Double-click -> installs for the current user (no admin prompt) -> opens Unplugged.
; Defines passed in by package.sh: VERSION, SRC (staged app folder), OUT (output .exe), ICON.

Unicode true
SetCompressor /SOLID lzma
SetCompressorDictSize 64
RequestExecutionLevel user
ManifestDPIAware true

Name "Unplugged"
Caption "Installing Unplugged"
OutFile "${OUT}"
Icon "${ICON}"
UninstallIcon "${ICON}"
InstallDir "$LOCALAPPDATA\Programs\Unplugged"
BrandingText "Keep AI Local · keepailocal.org"
ShowInstDetails nevershow
AutoCloseWindow true

VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "Unplugged"
VIAddVersionKey "FileDescription" "Unplugged setup: AI that lives entirely on your computer"
VIAddVersionKey "CompanyName" "Keep AI Local"
VIAddVersionKey "LegalCopyright" "MIT License (app). Gemma: Gemma Terms of Use."
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "ProductVersion" "${VERSION}"

!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\Unplugged"

Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Unplugged"
  ; replace any previous version cleanly
  RMDir /r "$INSTDIR"
  SetOutPath "$INSTDIR"
  File /r "${SRC}/*.*"

  WriteUninstaller "$INSTDIR\Uninstall Unplugged.exe"
  CreateShortcut "$SMPROGRAMS\Unplugged.lnk" "$INSTDIR\Unplugged.exe" "" "$INSTDIR\Unplugged.exe" 0
  CreateShortcut "$DESKTOP\Unplugged.lnk" "$INSTDIR\Unplugged.exe" "" "$INSTDIR\Unplugged.exe" 0

  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "Unplugged"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "Keep AI Local"
  WriteRegStr HKCU "${UNINST_KEY}" "URLInfoAbout" "https://keepailocal.org"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\Unplugged.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall Unplugged.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "EstimatedSize" 900000
SectionEnd

Function .onInstSuccess
  ; open Unplugged right away (skipped for silent installs: setup.exe /S)
  IfSilent +2
  Exec '"$INSTDIR\Unplugged.exe"'
FunctionEnd

Section "Uninstall"
  Delete "$SMPROGRAMS\Unplugged.lnk"
  Delete "$DESKTOP\Unplugged.lnk"
  DeleteRegKey HKCU "${UNINST_KEY}"
  RMDir /r "$INSTDIR"
SectionEnd
