###---------2024-03-01 ----------------------------------------

更新telnet 命令（默认端口666）：
0. 连接telnet service：telnet 127.0.0.1 666
1. 查看所有命令： izu -help  、 izu -? 
 
修改日志等级：log -r ruleName -min 2 -max 3
当前配置：show config
重载配置：show config -r

查看设备：show device
设备详细：show device deviceName

查看任务：show task
设备详细：show task id

###---------2024-01-16 ----------------------------------------

telnet 命令（默认端口666）：
0. 连接telnet service：telnet 127.0.0.1 666
1. 查看所有命令： izu -help  、 izu -? 
 
修改日志等级：log -r ruleName -min 2 -max 3
当前配置：show -i
重载配置：show -r

查看设备：show device
所有设备：show device -all
设备详细：show device deviceName

###---------2024-01-08 ----------------------------------------

★发布命令：
旧版：dotnet publish -c Release -r win-x64 /p:PublishsingleFile=false

新版发布全量包：dotnet publish -c Release -r win-x64 --self-contained
IIS中部署不支持单独文件（single file）

新版发布全量包，单独文件：
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false
windows服务部署可以使用此方式发布

指定发布文件夹
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false --output ./build

###---------2023-12-19 ----------------------------------------
更改windows service部署方式
安装命令：sc create izu-daemon binPath=绝对路径\IZU-Service.exe
删除命令：sc delete izu-daemon

###---------2023-12-07 ----------------------------------------
和设备联调
★增加了ActionType：
	心跳写回标记 SENDBACK  TRUE/FALSE 交替写回
	联机请求  ONLINE  仅写回TRUE

★增加了心跳写回机制

###---------2023-12-01 ----------------------------------------
★增加了服务器热重载功能。
	1. 服务配置热重载
	2. 变量表热重载

###---------2023-11-29 ----------------------------------------
★增加了通过websocket广播设备数据功能
★增加了在服务启动时, 自动将本地设备信息上传到izu数据库

###---------2023-11-27 ----------------------------------------
★增加从设备读取数据刷新时间的配置
★修改了从设备读取数据限制，仅在变量表中获取包含R标签的变量。
★增加设备数据变更后存储到本地文件
★完善服务信息和设备列表信息

###---------2023-11-22 ----------------------------------------
★修改了变量表的配置与读取，增加了1列标志ActionType（用于绑定特定的控制操作）
★增加与下位机通讯，建立断线重连机制
★重新整理日志记录，使排错条例更加清晰
★增加了服务启动时检测系统配置
★增加了灾后重启及其配置
★定义了HID, AUTODOOR, FIREDOOR三种设备
★增加了以上三种设备的控制器接口（控制启停等操作）

###---------2023-11-17 ----------------------------------------
★增加下位机变量表文件的读取
	文件存储在 DeviceTable 文件夹中，按设备分为多个csv文件
★优化了程序配置的加载方式 改为通过 IOptions 接口访问
★编写了一部分 S7 代码

###---------2023-11-16 ----------------------------------------
更新内容：
★建立数据池 DataPool，定义设备 Device
★增加csv读取方式，使用库 TinyCSVParser
★定义 Response 规则
	{
		"status": 0,
		"error": "",
		"data": null
	}
	说明：
	status=0 正常返回结果
	status>0 异常
	当status=0时，读取data
	当status>0时，读取error

★定义 IZU配置 （IZUCONFIG)
★新增接口 
	1. 查看服务器运行状态
	GET 无参 http://ipaddress:port/izu
	2. 查看设备测试数据
	GET 无参 http://ipaddress:port/izu/sample
	3. 查看设备
	GET 无参 http://ipaddress:port/izu/device?name=
	4. 查看设备列表
	GET 无参 http://ipaddress:port/izu/devices

###---------2023-11-15 ----------------------------------------
项目创建
本软件部署方式为 Windows Service
★项目部署：
	1. 安装
		程序根目录下运行cmd，输入命令：IZU_Daemon.exe install
		安装成功后可以在系统服务中查看（win+r，输入services.msc，服务名以 IZU 开头)
	1. 卸载
		程序根目录下运行cmd，输入命令：IZU_Daemon.exe uninstall

★项目日志：
	1. 文件放在程序根目录 logs 文件夹。（日志系统的异常文件存储在 C:\nlog-internal.log）
	2. 日志配置文件为 nlog.config 

★发布命令：dotnet publish -c Release -r win10-x64 /p:PublishsingleFile=false

