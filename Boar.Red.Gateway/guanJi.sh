netstat -antlp|grep 80
ps -ef |grep Boar.Red.Gateway|awk '{print $2}'|xargs kill -9
echo 服务已关机