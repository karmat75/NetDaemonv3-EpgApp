# Based on
- [NetDaemon v3](https://github.com/net-daemon/netdaemon)
- [NetDaemon V3 App Template](https://github.com/net-daemon/netdaemon-app-template) (via .Net CLI)
- [mutzl/netdaemon-epg](https://github.com/mutzl/netdaemon-epg)

# Requires
- [Home Assistant](https://www.home-assistant.io/)
- Mqtt Broker Addon for Home Assistant like Mosquitto broker 
- NetDaemon V3 Addon for Home Assistant
- NetDaemon V3 Integration (via HACS - Home Assistant Community Store)

# Using
1. Clone and open this repository in Visual Studio or Visual Studio Code
2. Rename *appsettings.example.json* to *appsettings.json* and edit for your needs (Sections: HomeAssistant, Mqtt)
3. Check [EpgApp/apps/Epg/Epg.yaml](/EpgApp/apps/Epg/Epg.yaml) and add or remove channels 
4. After start some sensors for choosen channels will be created in Home Assistant. You can search for in Settings/Devices & Services/Entities by the definied `SensorPrefix` in your [Epg.yaml](/EpgApp/apps/Epg/Epg.yaml) file.

# Sources
- Hörzu (german) - supported channels you will find in [EpgApp/apps/Epg/Services/HoerzuStation.cs](/EpgApp/apps/Epg/Services/HoerzuStation.cs)

# Config YAML explained
- `CleanupSensorsOnStartup` Boolean (true | false) - All sensors will be removed from Home Assistant on startup and recreated if it is needed. (usefull when channels will be changed)  
- `OnlyCleanupAndEnd` Boolean (true | false) - All sensors will be removed and application stops
- `RefreshrateInSeconds` Integer - Time in seconds the sensor state and attributes should be renewed (use lower values only for development)
- `SensorPrefix` String - each channel will be created as sensor and this value will be used as prefix for naming the sensor. Default: *epg*
- `DataProvider` Object List - List of used data provider (atm only Hoerzu is implemented)
  - `Fullname` String - The class name of the data provider
  - `Stations` String List - A list of station names from [HoerzuStation.cs](/EpgApp/apps/Epg/Services/HoerzuStation.cs)
  - `RefreshTimes` String List - A List of "Time Stamps" for planned general refresh of the EPG data.

# Hints
It will be usefull when you exclude your epg sensors from recording.
Add the following to your Home Assistant configuration.yaml:
```yaml
...
recorder:
  exclude:
    entity_globs:
      - sensor.epg_*
...
```
>Replace the prefix in **sensor.epg_*** with your value of `SensorPrefix`
