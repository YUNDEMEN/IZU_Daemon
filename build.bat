:: filepath: /C:/Code/IZU_Daemon/build.bat
:: 设置项目名称和 SonarQube 服务器信息
set projectName=IZU_Daemon
set sonarHostUrl=http://10.140.46.47:9000
set sonarToken=sqp_6ec3db9e71b02e2619c1a65ecbe699a7c4c8023d

:: 开始 SonarQube 分析
echo Starting SonarQube analysis...
dotnet sonarscanner begin /k:%projectName% /d:sonar.host.url=%sonarHostUrl% /d:sonar.token=%sonarToken%

:: 编译项目
echo Building project...

echo Building IZUProject...
dotnet build "IZUProject.sln"

echo Building IZU.Framework...
dotnet build "IZU.Framework.sln"

echo Building tools.sln...
dotnet build "tools.sln"

:: 结束 SonarQube 分析并上传结果
echo Ending SonarQube analysis and uploading results...
dotnet sonarscanner end /d:sonar.token=%sonarToken%

echo Build and SonarQube analysis completed.

pause