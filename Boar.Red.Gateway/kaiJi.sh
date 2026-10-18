netstat -antlp|grep 80
ps -ef |grep Boar.Red.Gateway|awk '{print $2}'|xargs kill -9
export DOTNET_ROOT=/opt/soft/.dotnet
export PATH=$PATH:/opt/soft/.dotnet
nohup dotnet Boar.Red.Gateway.dll >/dev/null 2>&1 &
echo "服务已启动"