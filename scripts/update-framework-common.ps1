# Framework.Common 동봉 DLL 갱신 스크립트
#
# https://github.com/hann2k/Lib.Net10 의 Release 폴더에서 빌드 산출물을 받아
# package/Lib.Net10/ 에 덮어쓴다. Directory.Build.props 의 HintPath 가 이 위치를
# 가리키므로, 라이브러리가 갱신되면 이 스크립트만 실행하면 된다.
#
# 사용법: powershell -ExecutionPolicy Bypass -File scripts\update-framework-common.ps1

$ErrorActionPreference = "Stop"

# raw.githubusercontent.com 기준 Release 폴더 URL (공개 저장소, 인증 불필요)
$baseUrl = "https://raw.githubusercontent.com/hann2k/Lib.Net10/main/Release"

# 저장 위치: <repoRoot>/package/Lib.Net10
$targetDir = Join-Path (Split-Path $PSScriptRoot -Parent) "package\Lib.Net10"
New-Item -ItemType Directory -Force $targetDir | Out-Null

# dll 은 필수, pdb/deps.json 은 있으면 받는다.
$required = @("Framework.Common.dll")
$optional = @("Framework.Common.pdb", "Framework.Common.deps.json", "Newtonsoft.Json.dll")

foreach ($file in $required) {
    $url = "$baseUrl/$file"
    Write-Host "다운로드: $url"
    Invoke-WebRequest -Uri $url -OutFile (Join-Path $targetDir $file) -UseBasicParsing
}

foreach ($file in $optional) {
    $url = "$baseUrl/$file"
    try {
        Invoke-WebRequest -Uri $url -OutFile (Join-Path $targetDir $file) -UseBasicParsing
        Write-Host "다운로드: $url"
    }
    catch {
        Write-Host "건너뜀(없음): $file"
    }
}

Write-Host "완료: $targetDir"
