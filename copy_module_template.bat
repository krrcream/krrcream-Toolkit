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

REM 复制并重命名Module.cs.template
echo 复制 Module.cs.template 到 %TARGET_DIR%\%MODULE_NAME%Module.cs
copy "%TEMPLATE_DIR%\Module.cs.template" "%TARGET_DIR%\%MODULE_NAME%Module.cs" >nul
powershell -Command "(Get-Content '%TARGET_DIR%\%MODULE_NAME%Module.cs') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '%TARGET_DIR%\%MODULE_NAME%Module.cs'"

REM 复制并重命名Options.cs.template
echo 复制 Options.cs.template 到 %TARGET_DIR%\%MODULE_NAME%Options.cs
copy "%TEMPLATE_DIR%\Options.cs.template" "%TARGET_DIR%\%MODULE_NAME%Options.cs" >nul
powershell -Command "(Get-Content '%TARGET_DIR%\%MODULE_NAME%Options.cs') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '%TARGET_DIR%\%MODULE_NAME%Options.cs'"

REM 复制并重命名ViewModel.cs.template
echo 复制 ViewModel.cs.template 到 %TARGET_DIR%\%MODULE_NAME%ViewModel.cs
copy "%TEMPLATE_DIR%\ViewModel.cs.template" "%TARGET_DIR%\%MODULE_NAME%ViewModel.cs" >nul
powershell -Command "(Get-Content '%TARGET_DIR%\%MODULE_NAME%ViewModel.cs') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '%TARGET_DIR%\%MODULE_NAME%ViewModel.cs'"

REM 复制并重命名View.cs.template
echo 复制 View.cs.template 到 %TARGET_DIR%\%MODULE_NAME%View.cs
copy "%TEMPLATE_DIR%\View.cs.template" "%TARGET_DIR%\%MODULE_NAME%View.cs" >nul
powershell -Command "(Get-Content '%TARGET_DIR%\%MODULE_NAME%View.cs') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '%TARGET_DIR%\%MODULE_NAME%View.cs'"

REM 复制并重命名ModuleName.cs.template
echo 复制 ModuleName.cs.template 到 %TARGET_DIR%\%MODULE_NAME%.cs
copy "%TEMPLATE_DIR%\ModuleName.cs.template" "%TARGET_DIR%\%MODULE_NAME%.cs" >nul
powershell -Command "(Get-Content '%TARGET_DIR%\%MODULE_NAME%.cs') -replace 'ModuleName', '%MODULE_NAME%' | Set-Content '%TARGET_DIR%\%MODULE_NAME%.cs'"

echo.
echo 模块 %MODULE_NAME% 创建完成！
echo.
echo 接下来请手动：
echo 1. 在 ToolModuleType 枚举中添加 %MODULE_NAME%
echo 2. 在 ConverterEnum 枚举中添加 %MODULE_NAME%
echo 3. 在 ToolModuleRegistry 中注册新模块
echo 4. 实现 %MODULE_NAME%.cs 中的转换逻辑
echo 5. 在 View.cs 中构建UI
echo 6. 在 Options.cs 中定义设置
echo.
pause