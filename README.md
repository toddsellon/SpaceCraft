![](https://raw.githubusercontent.com/toddsellon/SpaceCraft/main/thumbnail.jpg?token=AB6IMWOXRSLL2T76BAYSH327RTGKW)

# SpaceCraft
A mod for Space Engineers allowing AI controlled factions. Simply find the mod in the Steam Workshop or clone the repository into *Users\UserName\AppData\Roaming\SpaceEngineers\Mods\SpaceCraft* before launching the game and include it in the mod list for your game. You are free to reuse, modify, suggest, or even inquire about the code in any way including creating your own branches.

# Early Access
SpaceCraft is still a work in progress, so please expect issues/bugs/other frustrations. Don't hesitate to reach out should you encounter any difficulties and be as descriptive as possible so I can try to get to the bottom of any issues.

# Command Line Interface
SpaceCraft has a chat-based command interface that allows you to control SpaceCraft factions or change session settings. To use the CLI, simply type "/sc" into the chat, followed by your command. The following are the available commands (square brackets indicates optional arguments):

- **get**
	- Returns the value of a setting. All possible settings listed below.
	- /sc get difficulty
- **set**
	- Changes a configuration setting
	- /sc set engineers 2
- **join** faction
	- Joins a faction even if you haven't found them yet. Host player is force joined, others request invite.
	- /sc join ARC
- **gps** 
	- Adds a GPS marker for each Grid or Engineer controlled by your Faction
	- /sc gps
- **follow** [distance]
	- Orders all offensive grids in your faction to follow you. Optionally, specify how far away they should try to stay in meters (default 0).
	- /sc follow 600
- **stop** 
	- Stops your faction from following
	- /sc stop
- **build** "Prefab Name" [faction]
	- Orders the specified faction to begin construction of a specific prefab or orders your current faction if one is not specified.
	- /sc build "Terran SCV"
- **spawn** "Prefab Name" [faction]
	- Spawns a completed prefab for specified faction or your faction if one is not specified.
	- /sc spawn "Terran Battlecruiser" ARC
- **establish**
	- Establishes your faction as an AI controlled faction or whichever faction you specify. Faction must already exist. Add any faction specific flags after establish and/or include the -nodonate flag to donate all grids manually
	- /sc establish -aggressive
- **dissolve**
	- Removes an established faction
	- /sc dissolve factiontag
- **donate**
	- Donates the grid you're currently controlling to your faction. Be warned that the grid will count towards your Faction's grid limits and will impact its decision making.
	- /sc donate
- **respawn** [faction] [-remain]
	- Respawns target faction incase they glitched out or were added later into the game. Add the -remain flag to leave their grids behind for loot
	- /sc respawn ARC -remain
- **debug**
	- Toggles debug mode on/off
	- /sc debug
- **control** entityId faction
	- Tells SpaceCraft to control the specified entity. This is intended to be used by other mods.
	- /sc control 12345 ARC
- **release** entityId faction
	- Tells SpaceCraft to stop controlling the specified entity. This is intended to be used by other mods.
	- /sc release 12345 ARC
- **complete**
	- Tells your faction to complete the current building project (super laggy)
	- /sc complete
- **pay**
	- Pays off any outstanding balance for your faction. A balance is incurred whenever a construction project is started and
	the faction has to pay off the cost of one battery to avoid garbage collection.
	- /sc pay
- **reset**
	- Resets all quest progress
	- /sc reset
- **unlock**
	- Unlocks all technology
	- /sc unlock
- **lock**
	- Locks all technology
	- /sc lock
	
## Settings
These are the settings which can be changed
- difficulty (float) Multiplier for AI gathered resources (default 1)
- grids (int) Limit to how many grids each Faction will make (default 20)
- engineers (int) Limit of how many engineers each Faction should have (default 1)
- bots (int) Limit of how many bots each Faction should have (default 3)
- manualkits (bool) Allows admins to disable the automatic functionality on survival kits (default false)
- animations (bool) Enables or disables extra animations (default true)
- quests (bool) Enables or disables the quest system (default true)

Convars can also technically be changed inside your save folder in a file called "SCConvars.xml".

## Default Prefabs
- Terran Planet Pod
- Terran SCV
- Terran Outpost
- Terran MAR-1NE
- Terran Reaper
- Terran Cyclone
- Terran SCV (Atmo)
- Terran SCV (Space)
- Terran Command Center
- Terran Siege Tank
- Terran Battlecruiser
- Planetary Fortress
- Terran Wraith
- Protoss Outpost
- Protoss Probe
- Protoss Gateway
- Protoss Scout
- Protoss Nexus
- Protoss Tempest
- Protoss Stargate
- Zerg Drop Site
- Zerg Drone
- Zerg Hatchery
- Zerg Lair
- Zerg Mutalusk
- Zerg Hive
- Zerg Corruptor
(More to come, subject to change)


# Creating Factions
Factions are defined inside XML formated .sbc files *outside the SpaceCraft base mod*. Custom prefabs can also be added via XML.

# Example
The &lt;SpawnGroup&gt; **&lt;Description&gt;** is parsed using a Command Line syntax (separated by spaces). The first argument of "SpaceCraft" defines a new Faction. The Faction tag is the second argument. Optionally, an HSV color may be specified as the third argument. Also, any faction-specific parameters can optionally be defined with flags (i.e. -aggressive).

```xml
<?xml version="1.0"?>
<Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">

<SpawnGroups><SpawnGroup>
	<Id>
		<TypeId>SpawnGroupDefinition</TypeId>
		<SubtypeId>Archmages</SubtypeId>
	</Id>
	<Description>SpaceCraft ARC "0,-0.8,-0.306840628" -defensive</Description>
	<Icon>Textures\GUI\Icons\Fake.dds</Icon>
	<Frequency>1.0</Frequency>
	<IsPirate>true</IsPirate>
	<Prefabs>
		<Prefab SubtypeId="Terran Planet Pod">
			<Position>
				<X>0.0</X>
				<Y>0.0</Y>
				<Z>0.0</Z>
			</Position>
			<BeaconText>Terran Planet Pod</BeaconText>
			<Speed>20.0</Speed>
		</Prefab>
	</Prefabs>
</SpawnGroup></SpawnGroups>

<Factions><Faction Tag="ARC" Founder="Feldon Cane">
  <Id>
    <TypeId>MyObjectBuilder_FactionDefinition</TypeId>
    <SubtypeId>Archmages</SubtypeId>
  </Id>
  <DisplayName>Archmages</DisplayName>
  <IsDefault>true</IsDefault>
  <DefaultRelation>Neutral</DefaultRelation>
  <AcceptHumans>true</AcceptHumans>
  <AutoAcceptMember>true</AutoAcceptMember>
</Faction></Factions>

</Definitions>
```

## Faction Parameters
Faction parameters are still a work in progress but will allow factions to utilize different playstyles.
* protoss (Spawn as a Protoss)
* zerg (Spawn as a Zerg)
* aggressive (More likely to attack and build fighting units)
* defensive (Less likely to attack or build fighting units)
* outsider (Spawns on a different planet than the player)
* scavenger (Uses a grinder rather than drilling to aquire resources (not implemented yet))
* grounded (Does not build flying units (untested))
* static (Only builds static prefabs)
* nobuild (Does not build new prefabs and just does the best it can with existing prefabs)
* spawned (Is considered already spawned by SpaceCraft and therefore does not create a planet pod for this Faction)
* nuclear (This faction gains access to Uranium once it builds its first Refinery. This is intended for grounded factions who never reach space and get Uranium legitimately.)
More to come!



# Creating Prefabs
In order for SpaceCraft to use a prefab, it must be contained in a &lt;SpawnGroup&gt;, and its **&lt;Description&gt;** must begin with the word "SpaceCraft". If the SpawnGroup is also a Faction, the Prefabs included within (except the Faction's first SpawnGroup, which defines its spawn ship), will be proprietary to that Faction (untested). I will later add a Faction Parameter which forces a Faction to only use it own proprietary prefabs. This would open the possibility of other races.

## Example

This example adds a the "Terran SCV" prefab to all SpaceCraft factions because of its **&lt;Description&gt;**.

```xml
<?xml version="1.0"?>
<Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
<SpawnGroups>

<SpawnGroup>
	<Id>
		<TypeId>SpawnGroupDefinition</TypeId>
		<SubtypeId>SpaceCraft</SubtypeId>
	</Id>
	<Description>SpaceCraft</Description>
	<Icon>Textures\GUI\Icons\Fake.dds</Icon>
	<Frequency>1.0</Frequency>
	<IsPirate>true</IsPirate>
	<Prefabs>
		<Prefab SubtypeId="Terran SCV">
			<Position>
				<X>0.0</X>
				<Y>0.0</Y>
				<Z>0.0</Z>
			</Position>
			<BeaconText>Terran SCV</BeaconText>
			<Speed>0.0</Speed>
		</Prefab>
	</Prefabs>
</SpawnGroups>
</Definitions>
```

## Turning Blueprints into Prefabs

Simply open the .sbc file generated in your Blueprints folder and change the first and last few lines to match this:

```xml
<?xml version="1.0"?>
<Definitions xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Prefabs>
    <Prefab xsi:type="MyObjectBuilder_PrefabDefinition">
      <Id Type="MyObjectBuilder_PrefabDefinition" Subtype="Terran SCV" />
	  
	  
	  <!-- Everything in between is the same -->
	  
	  
	</Prefab>
  </Prefabs>
</Definitions>
```

Subgrids in prefabs are currently not working except wheels. If your Prefab has wheels, please delete the CubeGrids containing them. The mod does support subgrids there is just a bug in the spawn position or rotation of them. I've exhausted most of my ideas so far.

# Known Issues
* AI Engineers do not actually belong to the correct Faction. I have been unabled to resolve this yet despite spending many hours. Anyone know why it's not working? See SpaceCraft.Utils.Engineer Spawn()
* AI never expands/colonizes space (coming soon)
* AI is cheating by spawning in resources and instantly transferring them. This is actually a feature, not a bug. To improve performance, the AI does not actually destroy voxels, and I've limited the amount of moving required by the units. I plan to eventually add realistic cargo ship functionality but this didn't make the cut for early access.
* AI never actually attacks me (coming soon)
* Using mod gives a warning "Possible entity type script logic collision". I'm still not sure why this happens but mod still works.
* Opening saved games *created on day one* do not load propertly. This issue has been fixed but you must start a new game. Sorry about that!

# Coming Soon
- Better AI attack/move decision making
- Better support for faction flags (i.e. aggressive, scavenger)
- Space Colonization (+use of Uranium/Platinum)
- More realistic, less "cheaty" cargo/item transfer system(s)
- Bug fixes
- Support for other mods (technically compatible now but expect better integration eventually)
--WeaponCore
--Shield Mod(s)

## Coming later?
- I can't design the graphics, but I have created StarCraft to allow other races. Can anyone make Protoss and/or Zerg models?

# Special Thanks
A big thanks to the creators of the Modular Encounter Spawner and Rearth's NPCs. I would never have been able to create this mod without being able to review their code since this is my first mod ever. Good work guys!

## Subject to Change
