![](https://raw.githubusercontent.com/toddsellon/SpaceCraft/main/thumbnail.jpg?token=AB6IMWOXRSLL2T76BAYSH327RTGKW)

# SpaceCraft
A mod for Space Engineers

# Factions
Factions are defined inside Spawn Groups. Spawn Groups are defined inside XML formated .sbc files.

# Example
The &lt;SpawnGroup&gt; Description is parsed using a Command Line syntax. The first argument of "SpaceCraft" defines a new Faction. The Faction tag is the second argument. Any faction-specific parameters can be defined with flags (i.e. -aggressive).

```xml
<?xml version="1.0"?>
<Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
<SpawnGroups>

<SpawnGroup>
	<Id>
		<TypeId>SpawnGroupDefinition</TypeId>
		<SubtypeId>TerranPlanetPod</SubtypeId>
	</Id>
	<Description>SpaceCraft HVS -scavenger</Description>
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
