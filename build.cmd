@echo off
chcp 936 >nul
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (echo 未找到 .NET Framework 自带编译器 csc.exe & pause & exit /b 1)
for %%I in ("%CSC%") do set "REF=%%~dpI"
if not exist "app.ico" (echo 缺少图标文件 app.ico & pause & exit /b 1)
echo 正在编译 文件归档助手.exe ...
"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /win32icon:"app.ico" /resource:"app.ico",FileOrganizerApp.app.ico /out:"文件归档助手.exe" /win32manifest:"src\app.manifest" /reference:"%REF%System.dll" /reference:"%REF%System.Core.dll" /reference:"%REF%System.Drawing.dll" /reference:"%REF%System.Windows.Forms.dll" "src\Program.cs" "src\MainForm.cs" "src\FileOrganizer.cs" "src\DropPanel.cs"
if errorlevel 1 (echo 编译失败，请检查上方错误 & pause & exit /b 1)
echo 编译完成：文件归档助手.exe
pause