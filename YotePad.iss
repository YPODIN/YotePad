[Setup]
; Basic Application Info
AppName=YotePad
AppVersion=1.0.0
AppPublisher=Yann Perodin
AppCopyright=Copyright (C) 2026 Yann Perodin

; Handshake and Auto-Close Logic
CloseApplications=yes
CloseApplicationsFilter=*.exe
AppMutex=YotePadMutex

; Explicit Installer Metadata
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Yann Perodin
VersionInfoDescription=YotePad Installer
VersionInfoProductName=YotePad

; Installation Paths
DefaultDirName={autopf}\YotePad
; This will create an 'Output' folder in the same directory as this script
OutputDir=Output
OutputBaseFilename=YotePad-Installer

; Compiler Settings
DisableProgramGroupPage=yes
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes

[Files]
; Using a relative path so it works on any machine. 
; This assumes you are running the script from your project root.
Source: "bin\Release\net8.0-windows\publish\YotePad.exe"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
; 1. Nuke the AppData recovery folder and everything in it
Type: filesandordirs; Name: "{localappdata}\Yann Perodin\YotePad"

; 2. Nuke the Program Files folder and any extra files (logs, etc.)
Type: filesandordirs; Name: "{app}"

; 3. Clean up the parent author folder ONLY if it is now empty
Type: dirifempty; Name: "{localappdata}\Yann Perodin"

[Registry]
; 1. Nuke the entire Applications\YotePad.exe tree on uninstall
Root: HKCR; Subkey: "Applications\YotePad.exe"; Flags: uninsdeletekey
Root: HKCR; Subkey: "Applications\YotePad.exe\shell\open\command"; ValueType: string; ValueData: """{app}\YotePad.exe"" ""%1"""
Root: HKCR; Subkey: "Applications\YotePad.exe\SupportedTypes"; ValueType: string; ValueName: ".txt"; ValueData: ""
Root: HKCR; Subkey: "Applications\YotePad.exe\SupportedTypes"; ValueType: string; ValueName: ".log"; ValueData: ""
Root: HKCR; Subkey: "Applications\YotePad.exe\SupportedTypes"; ValueType: string; ValueName: ".ini"; ValueData: ""

; 2. Add to "App Paths" so Win+R works
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\YotePad.exe"; ValueType: string; ValueData: "{app}\YotePad.exe"; Flags: uninsdeletekey

; 3. Define the YotePad ProgID tree
Root: HKCR; Subkey: "YotePad.txt"; ValueType: string; ValueData: "Text Document"; Flags: uninsdeletekey
Root: HKCR; Subkey: "YotePad.txt\DefaultIcon"; ValueType: string; ValueData: "{app}\YotePad.exe,0"
Root: HKCR; Subkey: "YotePad.txt\shell\open\command"; ValueType: string; ValueData: """{app}\YotePad.exe"" ""%1"""

; 4. Clean up the specific OpenWith list values for the extensions
Root: HKCR; Subkey: ".txt\OpenWithProgids"; ValueType: string; ValueName: "YotePad.txt"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".log\OpenWithProgids"; ValueType: string; ValueName: "YotePad.txt"; ValueData: ""; Flags: uninsdeletevalue
Root: HKCR; Subkey: ".ini\OpenWithProgids"; ValueType: string; ValueName: "YotePad.txt"; ValueData: ""; Flags: uninsdeletevalue

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"; Flags: unchecked

[Icons]
Name: "{autoprograms}\YotePad"; Filename: "{app}\YotePad.exe"
Name: "{autodesktop}\YotePad"; Filename: "{app}\YotePad.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\YotePad.exe"; Description: "Launch YotePad"; Flags: nowait postinstall skipifsilent
