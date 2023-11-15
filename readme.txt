本软件部署方式为 Windows Service

项目部署：
1. 安装
	程序根目录下运行cmd，输入命令：IZU_Daemon.exe install
	安装成功后可以在系统服务中查看（win+r，输入services.msc，服务名以 IZU 开头)
1. 卸载
	程序根目录下运行cmd，输入命令：IZU_Daemon.exe uninstall

项目日志：
1. 文件放在程序根目录 logs 文件夹。（日志系统的异常文件存储在 C:\nlog-internal.log）
2. 日志配置文件为 nlog.config 