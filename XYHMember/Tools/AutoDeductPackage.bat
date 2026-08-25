@echo off
rem ============================================================
rem  套餐耗材 - 每日自动扣减（定时任务入口）
rem  由 Windows 计划任务每天 02:00 调用本脚本。
rem  默认扣减【昨天】的套餐使用记录，幂等（同一事件只扣一次）。
rem  如需补跑某区间，可在计划任务里加参数：
rem    AutoDeductPackage.bat 2026-08-20 2026-08-24
rem ============================================================
chcp 65001 >nul
setlocal

rem ---- 服务器地址，按实际部署环境修改 ----
set SERVER=http://172.68.1.12
set API_KEY=zrt-oa-2026
set URL=%SERVER%/OaApi/RunPackageAutoDeduct

set BDATE=%~1
set EDATE=%~2

echo [%date% %time%] 开始套餐自动扣减...
if "%BDATE%"=="" (
    echo 未指定日期区间，默认扣减昨天。
    curl -s -X POST "%URL%" -H "X-Api-Key: %API_KEY%"
) else (
    echo 补跑区间：%BDATE% ~ %EDATE%
    curl -s -X POST "%URL%" -H "X-Api-Key: %API_KEY%" --data-urlencode "bdate=%BDATE%" --data-urlencode "edate=%EDATE%"
)

echo.
echo [%date% %time%] 完成。
endlocal
