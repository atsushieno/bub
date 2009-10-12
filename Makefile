all : Pub.exe Sub.exe Hub.exe

PUB_SRC = Pub.cs PubSubHubBub.cs
SUB_SRC = Sub.cs PubSubHubBub.cs
HUB_SRC = Hub.cs PubSubHubBub.cs 

Pub.exe : $(PUB_SRC)
	gmcs -pkg:wcf $(PUB_SRC)
Sub.exe : $(SUB_SRC)
	gmcs -pkg:wcf $(SUB_SRC)
Hub.exe : $(HUB_SRC)
	gmcs -pkg:wcf $(HUB_SRC)
