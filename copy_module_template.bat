@echo off
REM 模块模板复制脚本
REM 用法: copy_module_template.bat <NewModuleName>

if "%1"=="" (
    echo 用法: copy_module_template.bat ^<NewModuleName^>
    echo 示例: copy_module_template.bat MyNewModule
    exit /b 1
)

set MODULE_NAME=%1
set TEMPLATE_DIR=tools\ModuleTemplate
set TARGET_DIR=tools\%MODULE_NAME%

echo 创建模块目录: %TARGET_DIR%
mkdir "%TARGET_DIR%" 2>nul

echo 复制模板文件...
for %%f in ("%TEMPLATE_DIR%\*.template") do (
    set "filename=%%~nf"
    set "target=%TARGET_DIR%\!filename:%ModuleName=%MODULE_NAME!.cs"
    echo 复制 %%f 到 !target!
    copy "%%f" "!target!" >nul

    echo 修改文件内容...
    powershell -Command "(Get-Content '!target!') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '!target!'"
)

echo.
echo 模块 %MODULE_NAME% 创建完成！
echo.
echo 接下来请手动：
echo 1. 在 ToolModuleType 枚举中添加 %MODULE_NAME%
echo 2. 在 ToolModuleRegistry 中注册新模块
echo 3. 实现 Processor.cs 中的转换逻辑
echo 4. 在 Control.cs 中构建UI
echo 5. 在 Options.cs 中定义设置
echo.
pause