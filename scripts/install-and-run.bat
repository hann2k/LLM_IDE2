@echo off
chcp 65001 >nul
setlocal enableextensions
title LLM IDE2 Writer

rem ============================================================
rem  LLM IDE2 Writer 설치 및 실행 (cmd 전용, PowerShell 미사용)
rem  - 이 배치파일과 LlmIde.Writer.zip 을 같은 폴더에 두고 더블클릭하세요.
rem ============================================================

set "HERE=%~dp0"
set "ZIP=%HERE%LlmIde.Writer.zip"
set "DATASRC=%HERE%LlmIde2"
set "DATADIR=%LOCALAPPDATA%\LlmIde2"
set "EXE=%HERE%LlmIde.Wpf.Writer.exe"

rem --- 언어 선택 / language selection ---
choice /c KE /n /m "언어 선택 / Select language:  [K] 한국어   [E] English  "
if errorlevel 2 (set "UILANG=EN") else (set "UILANG=KO")

if "%UILANG%"=="EN" (
    set "T_TITLE=LLM IDE2 Writer - Install and Run"
    set "T_S1=[1/5] Extracting package..."
    set "T_NOZIP=[Error] LlmIde.Writer.zip is not in this folder. Put this file and LlmIde.Writer.zip together, then run again."
    set "T_NOTAR=tar not found - using the built-in fallback extractor..."
    set "T_DONE=    Done."
    set "T_S2=[2/5] Moving data folder to:"
    set "T_NODATA=    No data folder (LlmIde2) in the package - skipping."
    set "T_S3=[3/5] System architecture:"
    set "T_S4=[4/5] Checking .NET 10 Desktop Runtime..."
    set "T_RTOK=    Already installed."
    set "T_RTNO=    Not installed - trying to install..."
    set "T_RTWG=    Installing via winget... (allow the UAC prompt)"
    set "T_RTWGD=    Install attempt finished."
    set "T_RTNOWG=    winget not found. Opening the official page - install .NET Desktop Runtime 10, then run this again."
    set "T_S5=[5/5] Launching Writer..."
    set "T_NOEXE=[Error] Executable not found:"
    set "T_FAIL=Setup did not complete. Please check the messages above."
) else (
    set "T_TITLE=LLM IDE2 Writer - 설치 및 실행"
    set "T_S1=[1/5] 압축파일 푸는 중..."
    set "T_NOZIP=[오류] 이 폴더에 LlmIde.Writer.zip 이 없습니다. 이 파일과 LlmIde.Writer.zip 을 같은 폴더에 두고 다시 실행하세요."
    set "T_NOTAR=tar 가 없어 내장 대체 방식으로 압축을 풉니다..."
    set "T_DONE=    완료."
    set "T_S2=[2/5] 데이터 폴더 이동:"
    set "T_NODATA=    패키지에 데이터 폴더(LlmIde2)가 없어 건너뜁니다."
    set "T_S3=[3/5] 시스템 아키텍처:"
    set "T_S4=[4/5] .NET 10 데스크톱 런타임 확인 중..."
    set "T_RTOK=    이미 설치됨."
    set "T_RTNO=    설치되어 있지 않습니다. 설치를 시도합니다..."
    set "T_RTWG=    winget 으로 설치 중... (UAC 창이 뜨면 허용)"
    set "T_RTWGD=    설치 시도 완료."
    set "T_RTNOWG=    winget 가 없어 공식 페이지를 엽니다. .NET 데스크톱 런타임 10 설치 후 다시 실행하세요."
    set "T_S5=[5/5] Writer 실행 중..."
    set "T_NOEXE=[오류] 실행 파일을 찾을 수 없습니다:"
    set "T_FAIL=설치를 완료하지 못했습니다. 위 안내를 확인하세요."
)

echo ============================================
echo   %T_TITLE%
echo ============================================
echo.

rem --- 1) 압축파일 풀기 (실행 폴더에) ---
echo %T_S1%
if not exist "%ZIP%" ( echo %T_NOZIP% & goto fail )
where tar >nul 2>nul
if %errorlevel%==0 (
    tar -xf "%ZIP%" -C "%HERE%."
) else (
    echo %T_NOTAR%
    call :unzip_vbs "%ZIP%" "%HERE:~0,-1%"
)
echo %T_DONE%
echo.

rem --- 2) 데이터 폴더를 %LOCALAPPDATA%\LlmIde2 로 이동 ---
echo %T_S2% "%DATADIR%"
if exist "%DATASRC%" (
    robocopy "%DATASRC%" "%DATADIR%" /E /MOVE >nul
    echo %T_DONE%
) else (
    echo %T_NODATA%
)
echo.

rem --- 3) 시스템 아키텍처 확인 (x86 / x64 / ARM64) ---
set "ARCH=%PROCESSOR_ARCHITECTURE%"
if defined PROCESSOR_ARCHITEW6432 set "ARCH=%PROCESSOR_ARCHITEW6432%"
set "DOTNETARCH=x64"
if /i "%ARCH%"=="x86" set "DOTNETARCH=x86"
if /i "%ARCH%"=="ARM64" set "DOTNETARCH=arm64"
echo %T_S3% %ARCH%  [.NET: %DOTNETARCH%]
echo.

rem --- 4) .NET 10 데스크톱 런타임 설치 여부 검사 ---
echo %T_S4%
set "HASRT="
for /f "delims=" %%R in ('dotnet --list-runtimes 2^>nul ^| findstr /C:"Microsoft.WindowsDesktop.App 10."') do set "HASRT=1"
if defined HASRT ( echo %T_RTOK% & goto run )
echo %T_RTNO%
where winget >nul 2>nul
if %errorlevel%==0 (
    echo %T_RTWG%
    winget install --id Microsoft.DotNet.DesktopRuntime.10 --architecture %DOTNETARCH% --accept-package-agreements --accept-source-agreements --silent
    echo %T_RTWGD%
    goto run
)
echo %T_RTNOWG%
start "" "https://dotnet.microsoft.com/download/dotnet/10.0/runtime?runtime=desktop"
goto fail

:run
rem --- 5) Writer 실행 ---
echo %T_S5%
if not exist "%EXE%" ( echo %T_NOEXE% "%EXE%" & goto fail )
start "" "%EXE%"
goto end

:fail
echo.
echo %T_FAIL%
pause
exit /b 1

rem --- WSH(VBScript) 폴백: tar 가 없는 구형 윈도우용 압축 해제 (PowerShell 미사용) ---
:unzip_vbs
set "VBS=%TEMP%\llmide_unzip.vbs"
if exist "%VBS%" del "%VBS%" >nul 2>nul
echo set a=WScript.Arguments >>"%VBS%"
echo sZip=a(0) >>"%VBS%"
echo sDst=a(1) >>"%VBS%"
echo set fso=CreateObject("Scripting.FileSystemObject") >>"%VBS%"
echo if not fso.FolderExists(sDst) then fso.CreateFolder(sDst) >>"%VBS%"
echo set sh=CreateObject("Shell.Application") >>"%VBS%"
echo set it=sh.NameSpace(sZip).Items() >>"%VBS%"
echo sh.NameSpace(sDst).CopyHere it, 16 >>"%VBS%"
echo do until sh.NameSpace(sDst).Items.Count ^>= it.Count >>"%VBS%"
echo WScript.Sleep 200 >>"%VBS%"
echo loop >>"%VBS%"
cscript //nologo "%VBS%" %1 %2
del "%VBS%" >nul 2>nul
exit /b

:end
endlocal
exit /b 0
