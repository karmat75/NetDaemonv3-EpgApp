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
4. Hit `F5` in Visual Studio to check the code. If all is fine, some sensors for choosen channels will be created in Home Assistant. You can search for in Settings/Devices & Services/Entities by the definied `SensorPrefix` in your [Epg.yaml](/EpgApp/apps/Epg/Epg.yaml) file.
5. Publish the code to your Home Assistant if all is fine for you. 

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
## Disable recording
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

## Lovelace card - single channel
![A Lovelace card which displays data for one channel](/Sample_SingleChannelCard.PNG)

You can use your EPG data in a lovelace markdown card.
Edit your dashboard and add an new card of type markdown. At the bottom of the card editor, click the button `SHOW CODE EDITOR` and past the following code.
```yaml
type: markdown
content: |-
  {% set entity_id = "sensor.epg_hoerzu_kabeleins" %}
  {% set durationpercent = 3  %}
  {% if states(entity_id) | int > durationpercent | int %}
    {% set durationpercent = states(entity_id) | int * 0.9 %}
  {% endif %}
  <table width="100%">
    <tr>
      <td colspan="3">
        <h3>{{ state_attr(entity_id, 'Station') }}</h3>
      </td>
    </tr>
    <tr>
      <td colspan="3">
        <strong>{{ state_attr(entity_id, 'Title') }}</strong>
      </td>
    </tr>
    <tr>
      <td width="35px">
        <i>{{ state_attr(entity_id, 'Start') }}&nbsp;&nbsp;</i>
      </td>
      <td title="{{states(entity_id)}}%">
        <img src="/local/images/cornflowerblue_pixel.PNG" width="4px" height="4px" /><img alt="{{durationpercent}}%" src="/local/images/cornflowerblue_pixel.PNG" width="{{states(entity_id)}}%" height="4px" />
      </td>
      <td width="35px">
        <i>&nbsp;&nbsp;{{ state_attr(entity_id, 'End') }}</i>
      </td>
    </tr>
    <tr>
      <td></td>
      <td colspan="2">
        <small>{{ state_attr(entity_id, 'Upcoming') }}</small>
      </td>
    </tr>
  </table>
```
>Replace in the first line under `content: |-` the value `sensor.epg_hoerzu_kabeleins` with an existing sensor name.

## Lovelace card - all channels
![A Lovelace card which displays data for multiple channels](/Sample_MultiChannelCard.PNG)

You can use your EPG data in a lovelace markdown card.
Edit your dashboard and add an new card of type markdown. At the bottom of the card editor, click the button `SHOW CODE EDITOR` and past the following code.
```yaml
type: markdown
content: |-
  {% set epg_prefix = "epg" %}
  {% for state in states.sensor %}
  {% if epg_prefix in state.entity_id %}
  {% set entity_id = state.entity_id %}
  {% set durationpercent = 3  %}
  {% if states(entity_id) | int > durationpercent | int %}
    {% set durationpercent = states(entity_id) | int * 0.9 %}
  {% endif %}
  <table width="100%">
    <tr>
      <td colspan="3">
        <h3>{{ state_attr(entity_id, 'Station') }}</h3>
      </td>
    </tr>
    <tr>
      <td colspan="3">
        <strong>{{ state_attr(entity_id, 'Title') }}</strong>
      </td>
    </tr>
    <tr>
      <td width="35px">
        <i>{{ state_attr(entity_id, 'Start') }}&nbsp;&nbsp;</i>
      </td>
      <td title="{{states(entity_id)}}%">
        <img src="/local/images/cornflowerblue_pixel.PNG" width="4px" height="4px" /><img alt="{{durationpercent}}%" src="/local/images/cornflowerblue_pixel.PNG" width="{{states(entity_id)}}%" height="4px" />
      </td>
      <td width="35px">
        <i>&nbsp;&nbsp;{{ state_attr(entity_id, 'End') }}</i>
      </td>
    </tr>
    <tr>
      <td></td>
      <td colspan="2">
        <small>{{ state_attr(entity_id, 'Upcoming') }}</small>
      </td>
    </tr>
  </table>
  {% endif %}
  {% endfor %}
```
>Replace in the first line under `content: |-` the value `epg` with the defined prefix for your epg sensors.

| :exclamation: Displaying many channels may result in a heavy load of your system and/or browser   |
|---------------------------------------------------------------------------------------------------|
