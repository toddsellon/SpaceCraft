# SpaceCraft
A mod for Space Engineers

# Factions
Factions are defined inside Spawn Groups. Spawn Groups are defined inside XML formated .sbc files.

# Example
&lt;?xml version="1.0"?&gt;
&lt;Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema"&gt;
&lt;SpawnGroups&gt;

&lt;SpawnGroup&gt;
	&lt;Id&gt;
		&lt;TypeId&gt;SpawnGroupDefinition&lt;/TypeId&gt;
		&lt;SubtypeId&gt;TerranPlanetPod&lt;/SubtypeId&gt;
	&lt;/Id&gt;
	&lt;Description&gt;SpaceCraft HVS -scavenger&lt;/Description&gt;
	&lt;Icon&gt;Textures\GUI\Icons\Fake.dds&lt;/Icon&gt;
	&lt;Frequency&gt;1.0&lt;/Frequency&gt;
	&lt;IsPirate&gt;true&lt;/IsPirate&gt;
	&lt;Prefabs&gt;
		&lt;Prefab SubtypeId="TerranPlanetPod"&gt;
			&lt;Position&gt;
				&lt;X&gt;0.0&lt;/X&gt;
				&lt;Y&gt;0.0&lt;/Y&gt;
				&lt;Z&gt;0.0&lt;/Z&gt;
			&lt;/Position&gt;
			&lt;BeaconText&gt;Terran Planet Pod&lt;/BeaconText&gt;
			&lt;Speed&gt;20.0&lt;/Speed&gt;
		&lt;/Prefab&gt;
	&lt;/Prefabs&gt;
&lt;/SpawnGroup&gt;



&lt;/SpawnGroups&gt;
&lt;/Definitions&gt;
