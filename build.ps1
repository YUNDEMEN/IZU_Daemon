# 设置项目名称和 SonarQube 服务器信息
$projectName = "IZU_Daemon"
$sonarHostUrl = "http://10.140.46.47:9000"
$sonarToken = "sqp_6ec3db9e71b02e2619c1a65ecbe699a7c4c8023d"

# 开始 SonarQube 分析
Write-Host "Starting SonarQube analysis..."
dotnet sonarscanner begin /k:$projectName /d:sonar.host.url=$sonarHostUrl /d:sonar.token=$sonarToken

# 编译项目
Write-Host "Building project..."

Write-Host "Building IZUProject..."
dotnet build "IZUProject.sln"

Write-Host "Building IZU.Framework..."
dotnet build "IZU.Framework.sln"

Write-Host "Building tools.sln..."
dotnet build "tools.sln"


dotnet build $solutionFile

# 结束 SonarQube 分析并上传结果
Write-Host "Ending SonarQube analysis and uploading results..."
dotnet sonarscanner end /d:sonar.token=$sonarToken

Write-Host "Build and SonarQube analysis completed."
