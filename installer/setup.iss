; Inno Setup Script for MTE Stock
; Developer: م. مصطفى طلعت | 01116626164

[Setup]
AppName=MTE Stock
AppVersion=1.0
AppPublisher=م. مصطفى طلعت
AppPublisherURL=https://github.com/anomalyco
AppContact=01116626164
DefaultDirName={autopf64}\MTE Stock
DefaultGroupName=MTE Stock
UninstallDisplayIcon={app}\ProductApp.exe
Compression=lzma2/max
SolidCompression=yes
OutputDir=.
OutputBaseFilename=MTEStock_Setup_V1
SetupIconFile=..\app.ico
UninstallDisplayName=MTE Stock
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
DisableReadyPage=no
Password=6324919
Encryption=yes

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Messages]
PasswordLabel1=هذا البرنامج محمي بكلمة مرور
PasswordLabel3=الرجاء إدخال كلمة المرور الخاصة بالمثبت للمتابعة.%n%nإذا كنت لا تملك كلمة المرور، يرجى التواصل مع المطور.%n%nالمهندس مصطفى طلعت%n01116626164
PasswordEditLabel=كلمة المرور:
ClickNext=انقر فوق "التالي" للمتابعة، أو "إلغاء" للخروج.
ReadyLabel2a=تم تصميم وتطوير هذا النظام بواسطة%nالمهندس مصطفى طلعت - للحلول البرمجية%n01116626164%n%nسيتم تثبيت البرنامج في المسار التالي:

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\MTE Stock"; Filename: "{app}\ProductApp.exe"; WorkingDir: "{app}"; Comment: "MTE Stock - نظام إدارة المخزون والمبيعات"
Name: "{autodesktop}\MTE Stock"; Filename: "{app}\ProductApp.exe"; WorkingDir: "{app}"; Comment: "MTE Stock - نظام إدارة المخزون والمبيعات"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "إنشاء اختصار على سطح المكتب"; GroupDescription: "اختصارات إضافية:"

[Run]
Filename: "{app}\ProductApp.exe"; Description: "تشغيل MTE Stock"; Flags: postinstall nowait skipifsilent shellexec

[Code]
var
  UninstallResult: Integer;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    // إغلاق البرنامج أولاً
    Exec('cmd', '/c taskkill /f /im ProductApp.exe 2>nul', '', SW_HIDE, ewWaitUntilTerminated, UninstallResult);
    Sleep(1000);
    // حذف قاعدة البيانات من مجلد AppData
    Exec('cmd', '/c if exist "' + ExpandConstant('{localappdata}') + '\MTE Stock\inventory.db" del /f /q "' + ExpandConstant('{localappdata}') + '\MTE Stock\inventory.db"', '', SW_HIDE, ewWaitUntilTerminated, UninstallResult);
  end;
end;
