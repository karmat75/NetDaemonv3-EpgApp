# Based on
- [NetDaemon v3](https://github.com/net-daemon/netdaemon)
- [mutzl/netdaemon-epg](https://github.com/mutzl/netdaemon-epg)

# Requires
- [Home Assistant](https://www.home-assistant.io/)
- Mqtt Broker Addon for Home Assistant like Mosquitto broker 
- NetDaemon V3 Addon for Home Assistant
- NetDaemon V3 Integration (via HACS - Home Assistant Community Store)

# Using
1. Clone and open this repository in Visual Studio or Visual Studio Code
2. Rename appsettings.example.json and edit for your needs (Sections: HomeAssistnat, Mqtt)
3. Check ./apps/Epg/EPG.yaml and add or remove channels 

# Sources
- Hörzu (german) - supported channels you will find in [EpgApp/apps/Epg/Services/HoerzuStation.cs](/EpgApp/apps/Epg/Services/HoerzuStation.cs)