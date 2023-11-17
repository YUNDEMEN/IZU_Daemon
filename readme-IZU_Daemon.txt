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

★发布命令：dotnet publish -c Release-r win10-x64 /p:PublishsingleFile=false

