![](https://raw.githubusercontent.com/toddsellon/SpaceCraft/main/thumbnail.jpg?token=AB6IMWOXRSLL2T76BAYSH327RTGKW)

# SpaceCraft
A mod for Space Engineers. Simply include the clone the repository into *Users\UserName\AppData\Roaming\SpaceEngineers\Mods\SpaceCraft* before launching the game and include it in the mod list for your game.

# Testing
You can test in a limited fashion by loading a new **Custom Game** on the **Alien Planet** (untested on other worlds). Use the Entity List (Alt + F10) to locate the AI grids.

# Factions
Factions are defined inside Spawn Groups. Spawn Groups are defined inside XML formated .sbc files.

# Example
The &lt;SpawnGroup&gt; Description is parsed using a Command Line syntax. The first argument of "SpaceCraft" defines a new Faction. The Faction tag is the second argument. Optionally, an HSV color may be specified as the first argument. Also, any faction-specific parameters can optionally be defined with flags (i.e. -aggressive).

```xml
<?xml version="1.0"?>
<Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
<SpawnGroups>

<SpawnGroup>
	<Id>
		<TypeId>SpawnGroupDefinition</TypeId>
		<SubtypeId>Cavalry</SubtypeId>
	</Id>
	<Description>SpaceCraft HVS "207.4,80.0,45.0" -scavenger</Description>
	<Icon>Textures\GUI\Icons\Fake.dds</Icon>
	<Frequency>1.0</Frequency>
	<IsPirate>true</IsPirate>
	<Prefabs>
		<Prefab SubtypeId="TerranPlanetPod">
			<Position>
				<X>0.0</X>
				<Y>0.0</Y>
				<Z>0.0</Z>
			</Position>
			<BeaconText>Terran Planet Pod</BeaconText>
			<Speed>20.0</Speed>
		</Prefab>
	</Prefabs>
</SpawnGroup>



</SpawnGroups>
</Definitions>
```

## Subject to Change
