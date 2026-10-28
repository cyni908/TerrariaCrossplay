# What is Crossplay?
Crossplay allows for cross-platform play between all 1.4.5+ versions, utlimately closing the gap between Terraria on mobile and PC devices. This plugin works by modifying incoming packets sent from the client (and outgoing packets sent from the server) to match whatever game version the packets are being sent to. 

## Installation
Installation is very easy; Simply insert the plugin file (`Crossplay.dll`) into the `ServerPlugins` folder of your TShock install.

## Bugs & Issues
Bugs or other issues with this plugin should be reported as an issue to this page to myself.

## Supported versions

The plugin translates any client between 1.4.5.0 (release `313`) and one release
below the running server. Clients on the server's own release are left alone,
and newer clients are refused by vanilla.

| Client | Release | Verified |
| --- | --- | --- |
| v1.4.5.0 – v1.4.5.6 | 313 – 319 | 1.4.5.6 tested |
| v1.4.5.7 | 325 | tested on mobile |
| v1.4.5.8 | 326 | server version |

## Configuration

`tshock/Crossplay.json` is created on first start:

```json
{
  "support_journey_clients": false,
  "debug_mode": false,
  "strict_version_check": false
}
```

- `support_journey_clients` – allow journey-mode clients on this server, and
  strip journey mode when the server is not creative.
- `debug_mode` – log the raw version string each client reports.
- `strict_version_check` – refuse to start when the server build is newer than
  the newest one this fork knows about. Off by default, because the connect
  rewrite does not depend on the server build.

