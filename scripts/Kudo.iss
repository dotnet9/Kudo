; Kudo Windows 安装包（Inno Setup 6）。
; 从仓库根目录编译：ISCC.exe scripts\Kudo.iss /DAppVersion=x.y.z /DSourceDir=..\artifacts\publish\win-x64\Kudo.Demo.Desktop

#ifndef AppVersion
#define AppVersion "0.0.0.1"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64\Kudo.Demo.Desktop"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\release"
#endif

[Setup]
LicenseFile=..\LICENSE
AppId={{7A4D22E1-6F2C-4B03-9B58-24D0F8C1A771}
AppName=Kudo
AppVersion={#AppVersion}
AppPublisher=Dotnet9
SetupIconFile=..\logo.ico
AppSupportURL=https://github.com/dotnet9/Kudo/issues
DefaultDirName={autopf}\Kudo
DefaultGroupName=Kudo
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=Kudo-v{#AppVersion}-win-x64-setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; 按用户安装：应用以普通权限运行，可持久化自身设置（配置文件在安装目录内）。
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=Kudo.Demo.Desktop.exe
UninstallDisplayIcon={app}\Kudo.Demo.Desktop.exe
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Kudo"; Filename: "{app}\Kudo.Demo.Desktop.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Kudo"; Filename: "{app}\Kudo.Demo.Desktop.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; WorkingDirectory 必须指向 {app}：否则 Avalonia+SkiaSharp 在下载/临时目录初始化会崩溃。
Filename: "{app}\Kudo.Demo.Desktop.exe"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,Kudo}"; Flags: runasoriginaluser nowait postinstall skipifsilent
