; Instalador do improve para Windows (Inno Setup 6).
; Uso: iscc /DAppVersion=1.2.3 /DFileVersion=1.2.3 /DSourceDir=<pasta do dotnet publish> /DOutputDir=<saída> installer\windows\improve.iss

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installers"
#endif

[Setup]
AppId={{6B3F9A52-7C1D-4E8B-9A2F-1D5C7E3B4A60}
AppName=improve
AppVersion={#AppVersion}
AppVerName=improve {#AppVersion}
; A versão do arquivo só aceita números (a AppVersion pode ter sufixo, como 0.3.0-dev.12).
VersionInfoVersion={#FileVersion}
AppPublisher=improve
DefaultDirName={autopf}\improve
DefaultGroupName=improve
DisableProgramGroupPage=yes
; Instala só para o usuário atual, sem pedir administrador (o início automático também é por usuário).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=improve-setup-x64
SetupIconFile=..\..\src\Improve.App\Assets\AppIcon\improve.ico
UninstallDisplayIcon={app}\improve.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Fecha o improve aberto antes de atualizar os arquivos.
CloseApplications=force

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\improve"; Filename: "{app}\improve.exe"
Name: "{autodesktop}\improve"; Filename: "{app}\improve.exe"; Tasks: desktopicon

[Registry]
; O app grava "Iniciar com o sistema" nesta chave; a desinstalação remove o valor, se existir.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "improve"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\improve.exe"; Description: "{cm:LaunchProgram,improve}"; Flags: nowait postinstall skipifsilent
