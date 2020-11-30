using System.Collections.Generic;
using VRageMath;
using VRage;
using VRage.Game.Entity;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Definitions;

namespace SpaceCraft.Utils {

  public class Zerg {

    private static Zerg instance;

    public static Zerg Static
    {
      get {
        if( instance == null ) {
          instance = new Zerg();
        }
        return instance;
      }
    }

    private static readonly MyDefinitionId None =	MyDefinitionId.Parse("MyObjectBuilder_Ore/Iron");

    private static readonly string[] BlockNames = {
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicBlock",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeBlockCubeBlocksSlope",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicCorner",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicCornerInv",
      "MyObjectBuilder_CubeBlock/LargeRoundOrganic_Slope",
      "MyObjectBuilder_CubeBlock/LargeRoundOrganic_Corner",
      "MyObjectBuilder_CubeBlock/LargeRoundOrganic_CornerInv",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicSlope",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicCorner",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicCornerInv",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicBlock",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicSlope",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicCorner",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicCornerInv",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicBlock",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicSlope",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicCorner",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicCornerInv",
      "MyObjectBuilder_CubeBlock/LargeHalfOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeHeavyHalfOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeHalfSlopeOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeHeavyHalfSlopeOrganicBlock",
      "MyObjectBuilder_CubeBlock/HalfOrganicBlock",
      "MyObjectBuilder_CubeBlock/HeavyHalfOrganicBlock",
      "MyObjectBuilder_CubeBlock/HalfSlopeOrganicBlock",
      "MyObjectBuilder_CubeBlock/HeavyHalfSlopeOrganicBlock",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicRoundSlope",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicRoundCorner",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicRoundCornerInv",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicRoundSlope",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicRoundCorner",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicRoundCornerInv",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicRoundSlope",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicRoundCorner",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicRoundCornerInv",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicRoundSlope",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicRoundCorner",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicRoundCornerInv",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicSlope2Base",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicSlope2Tip",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicCorner2Base",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicCorner2Tip",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicInvCorner2Base",
      "MyObjectBuilder_CubeBlock/LargeBlockOrganicInvCorner2Tip",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicSlope2Base",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicSlope2Tip",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicCorner2Base",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicCorner2Tip",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicInvCorner2Base",
      "MyObjectBuilder_CubeBlock/LargeHeavyBlockOrganicInvCorner2Tip",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicSlope2Base",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicSlope2Tip",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicCorner2Base",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicCorner2Tip",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicInvCorner2Base",
      "MyObjectBuilder_CubeBlock/SmallBlockOrganicInvCorner2Tip",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicSlope2Base",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicSlope2Tip",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicCorner2Base",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicCorner2Tip",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicInvCorner2Base",
      "MyObjectBuilder_CubeBlock/SmallHeavyBlockOrganicInvCorner2Tip",
    };


    public List<MyCubeBlockDefinition> Blocks = new List<MyCubeBlockDefinition>();

    private Zerg() {
      foreach( string name in BlockNames ) {
        MyDefinitionId id = None;
        if( !MyDefinitionId.TryParse(name, out id) ) continue;

        MyCubeBlockDefinition def = null;

        if( MyDefinitionManager.Static.TryGetCubeBlockDefinition (id, out def) ) {
          Blocks.Add( def );
        }


      }
    }

    public bool IsZerg( IMySlimBlock slim ) {
      return Blocks.Contains(slim.BlockDefinition as MyCubeBlockDefinition);
    }




  }

}
